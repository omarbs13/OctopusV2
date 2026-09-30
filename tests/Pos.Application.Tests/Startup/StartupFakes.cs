using Pos.Application.Startup;

namespace Pos.Application.Tests.Startup;

/// <summary>Registro compartido del orden de las llamadas de los dobles de prueba.</summary>
public sealed class CallLog
{
    public List<string> Calls { get; } = [];
}

public sealed class FakeDatabaseMaintenance(CallLog log) : IDatabaseMaintenance
{
    public bool Exists { get; set; } = true;

    public IntegrityState Integrity { get; set; } = IntegrityState.Ok;

    public MigrationState Migrations { get; set; } = MigrationState.UpToDate;

    public Exception? MigrateException { get; set; }

    public long SizeBytes { get; set; } = 1_000;

    public long FreeSpaceBytes { get; set; } = 1_000_000;

    public bool DatabaseExists()
    {
        log.Calls.Add("Exists");
        return Exists;
    }

    public Task<IntegrityState> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        log.Calls.Add("CheckIntegrity");
        return Task.FromResult(Integrity);
    }

    public Task<MigrationState> GetMigrationStateAsync(CancellationToken cancellationToken)
    {
        log.Calls.Add("GetMigrationState");
        return Task.FromResult(Migrations);
    }

    public Task MigrateAsync(CancellationToken cancellationToken)
    {
        log.Calls.Add("Migrate");
        return MigrateException is null ? Task.CompletedTask : Task.FromException(MigrateException);
    }

    public Task EnableWalAsync(CancellationToken cancellationToken)
    {
        log.Calls.Add("EnableWal");
        return Task.CompletedTask;
    }

    public long DatabaseSizeBytes() => SizeBytes;

    public long AvailableFreeSpaceBytes() => FreeSpaceBytes;
}

public sealed class FakeBackupService(CallLog log) : IBackupService
{
    public Dictionary<BackupKind, BackupInfo?> Latest { get; } = new()
    {
        [BackupKind.Automatic] = null,
        [BackupKind.PreMigration] = null,
    };

    public Dictionary<BackupKind, Exception?> CreateExceptions { get; } = new()
    {
        [BackupKind.Automatic] = null,
        [BackupKind.PreMigration] = null,
    };

    public List<BackupInfo> Restored { get; } = [];

    /// <summary>Retraso artificial al crear, para probar el límite de tiempo al cerrar.</summary>
    public TimeSpan CreateDelay { get; set; } = TimeSpan.Zero;

    public async Task<BackupInfo> CreateAsync(BackupKind kind, CancellationToken cancellationToken)
    {
        log.Calls.Add($"Backup:{kind}");
        if (CreateDelay > TimeSpan.Zero)
        {
            await Task.Delay(CreateDelay, CancellationToken.None);
        }

        if (CreateExceptions[kind] is { } ex)
        {
            throw ex;
        }

        var info = new BackupInfo($"/backups/{kind}.db", kind, new DateTime(2026, 9, 29, 15, 30, 0, DateTimeKind.Utc));
        Latest[kind] = info;
        return info;
    }

    public Task<BackupInfo?> GetLatestAsync(BackupKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(Latest[kind]);

    public Task RestoreAsync(BackupInfo backup, CancellationToken cancellationToken)
    {
        log.Calls.Add($"Restore:{backup.Kind}");
        Restored.Add(backup);
        return Task.CompletedTask;
    }

    public Task QuarantineCurrentDatabaseAsync(CancellationToken cancellationToken)
    {
        log.Calls.Add("Quarantine");
        return Task.CompletedTask;
    }

    public Task<string> CreateTemporaryCopyAsync(CancellationToken cancellationToken) =>
        Task.FromResult("/tmp/copy.db");
}
