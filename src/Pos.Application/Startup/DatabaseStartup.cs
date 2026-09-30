using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;

namespace Pos.Application.Startup;

/// <summary>
/// Secuencia de arranque de la base en el orden obligatorio de la constitución (Principio IV):
/// la instancia única ya se verificó; se detecta una base más nueva; se respalda; se migra y, si
/// falla, se restaura el respaldo y no se continúa. Ver contracts/startup.md.
/// </summary>
public sealed partial class DatabaseStartup : IDatabaseStartup
{
    public static readonly TimeSpan AutomaticBackupInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan DefaultCloseBackupTimeout = TimeSpan.FromSeconds(15);

    private readonly IDatabaseMaintenance _database;
    private readonly IBackupService _backups;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseStartup> _logger;
    private readonly TimeSpan _closeBackupTimeout;

    public DatabaseStartup(
        IDatabaseMaintenance database,
        IBackupService backups,
        IClock clock,
        ILogger<DatabaseStartup> logger)
        : this(database, backups, clock, logger, DefaultCloseBackupTimeout)
    {
    }

    public DatabaseStartup(
        IDatabaseMaintenance database,
        IBackupService backups,
        IClock clock,
        ILogger<DatabaseStartup> logger,
        TimeSpan closeBackupTimeout)
    {
        _database = database;
        _backups = backups;
        _clock = clock;
        _logger = logger;
        _closeBackupTimeout = closeBackupTimeout;
    }

    public async Task<StartupResult> RunAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress = null)
    {
        var watch = Stopwatch.StartNew();
        var result = await RunStepsAsync(cancellationToken, progress);
        LogStartupFinished(result.GetType().Name, watch.ElapsedMilliseconds);
        return result;
    }

    public async Task RecoverFromBackupAsync(BackupInfo backup, CancellationToken cancellationToken, IProgress<StartupStep>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(backup);
        progress?.Report(StartupStep.Restoring);
        LogRecovering(backup.Path, backup.CreatedAtUtc);
        await _backups.QuarantineCurrentDatabaseAsync(cancellationToken);
        await _backups.RestoreAsync(backup, cancellationToken);
    }

    public async Task BackupOnCloseAsync()
    {
        using var timeout = new CancellationTokenSource(_closeBackupTimeout);
        var backup = TryAutomaticBackupAsync(timeout.Token, progress: null);
        var finished = await Task.WhenAny(backup, Task.Delay(_closeBackupTimeout, CancellationToken.None));
        if (finished != backup)
        {
            LogCloseBackupTimedOut(_closeBackupTimeout.TotalSeconds);
        }
    }

    private async Task<StartupResult> RunStepsAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress)
    {
        try
        {
            progress?.Report(StartupStep.CheckingDatabase);
            if (!_database.DatabaseExists())
            {
                LogCreatingDatabase();
                progress?.Report(StartupStep.Migrating);
                await _database.MigrateAsync(cancellationToken);
            }
            else
            {
                var problem = await CheckExistingDatabaseAsync(cancellationToken, progress);
                if (problem is not null)
                {
                    return problem;
                }
            }

            await _database.EnableWalAsync(cancellationToken);
            await TryAutomaticBackupAsync(cancellationToken, progress);
            progress?.Report(StartupStep.Finishing);
            return new StartupResult.Ready();
        }
        catch (DatabaseAccessException ex)
        {
            LogAccessProblem(ex, ex.Problem);
            return ToResult(ex.Problem);
        }
    }

    private async Task<StartupResult?> CheckExistingDatabaseAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress)
    {
        var integrity = await _database.CheckIntegrityAsync(cancellationToken);
        LogIntegrity(integrity);
        switch (integrity)
        {
            case IntegrityState.Corrupted:
                return new StartupResult.Corrupted(await _backups.GetLatestAsync(BackupKind.Automatic, cancellationToken));
            case IntegrityState.Locked:
                return new StartupResult.Inaccessible(DatabaseProblem.Locked);
            case IntegrityState.PermissionDenied:
                return new StartupResult.Inaccessible(DatabaseProblem.PermissionDenied);
        }

        var migrations = await _database.GetMigrationStateAsync(cancellationToken);
        LogMigrationState(migrations.Status, migrations.Migrations);
        return migrations.Status switch
        {
            MigrationStatus.Newer => new StartupResult.NewerDatabase(),
            MigrationStatus.Pending => await MigrateWithBackupAsync(cancellationToken, progress),
            _ => null,
        };
    }

    private async Task<StartupResult?> MigrateWithBackupAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress)
    {
        var required = 2 * _database.DatabaseSizeBytes();
        var available = _database.AvailableFreeSpaceBytes();
        if (available < required)
        {
            LogInsufficientSpace(available, required);
            return new StartupResult.InsufficientSpace();
        }

        progress?.Report(StartupStep.BackingUp);
        var backup = await _backups.CreateAsync(BackupKind.PreMigration, cancellationToken);
        LogStep("Respaldo previo a migrar", backup.Path);

        try
        {
            progress?.Report(StartupStep.Migrating);
            await _database.MigrateAsync(cancellationToken);
            LogStep("Migración", "aplicada");
            return null;
        }
#pragma warning disable CA1031 // Cualquier falla de migración debe terminar en restauración del respaldo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogMigrationFailed(ex, backup.Path);
            progress?.Report(StartupStep.Restoring);
            try
            {
                await _backups.RestoreAsync(backup, CancellationToken.None);
                LogStep("Restauración tras falla de migración", backup.Path);
            }
#pragma warning disable CA1031
            catch (Exception restoreEx)
#pragma warning restore CA1031
            {
                LogRestoreFailed(restoreEx, backup.Path);
            }

            return new StartupResult.MigrationFailed();
        }
    }

    private async Task TryAutomaticBackupAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress)
    {
        try
        {
            var latest = await _backups.GetLatestAsync(BackupKind.Automatic, cancellationToken);
            if (latest is not null && _clock.UtcNow - latest.CreatedAtUtc <= AutomaticBackupInterval)
            {
                return;
            }

            progress?.Report(StartupStep.BackingUp);
            var backup = await _backups.CreateAsync(BackupKind.Automatic, cancellationToken);
            LogStep("Respaldo automático", backup.Path);
        }
#pragma warning disable CA1031 // Un respaldo automático fallido no debe impedir usar la aplicación.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogAutomaticBackupFailed(ex);
        }
    }

    private static StartupResult ToResult(DatabaseProblem problem) => problem switch
    {
        DatabaseProblem.DiskFull => new StartupResult.InsufficientSpace(),
        DatabaseProblem.Corrupted => new StartupResult.Corrupted(null),
        _ => new StartupResult.Inaccessible(problem),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Arranque de la base terminado: {Result} en {ElapsedMs} ms")]
    private partial void LogStartupFinished(string result, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arranque: {Step} → {Outcome}")]
    private partial void LogStep(string step, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arranque: verificación de integridad → {Integrity}")]
    private partial void LogIntegrity(IntegrityState integrity);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arranque: estado de migraciones → {Status} {Migrations}")]
    private partial void LogMigrationState(MigrationStatus status, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "No existe la base de datos; se creará")]
    private partial void LogCreatingDatabase();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Espacio insuficiente para migrar: disponible {Available} bytes, requerido {Required} bytes")]
    private partial void LogInsufficientSpace(long available, long required);

    [LoggerMessage(Level = LogLevel.Error, Message = "Problema de acceso a la base de datos: {Problem}")]
    private partial void LogAccessProblem(Exception exception, DatabaseProblem problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló la migración; se restaurará el respaldo {Backup}")]
    private partial void LogMigrationFailed(Exception exception, string backup);

    [LoggerMessage(Level = LogLevel.Critical, Message = "No se pudo restaurar el respaldo {Backup} tras la falla de migración")]
    private partial void LogRestoreFailed(Exception exception, string backup);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falló el respaldo automático")]
    private partial void LogAutomaticBackupFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El respaldo al cerrar excedió {Seconds} s; la aplicación cierra igual")]
    private partial void LogCloseBackupTimedOut(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Restaurando la base dañada desde {Backup} ({CreatedAtUtc:u})")]
    private partial void LogRecovering(string backup, DateTime createdAtUtc);
}
