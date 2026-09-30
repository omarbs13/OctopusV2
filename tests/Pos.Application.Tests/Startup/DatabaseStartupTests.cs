using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Startup;
using Pos.Application.Tests.TestSupport;

namespace Pos.Application.Tests.Startup;

public class DatabaseStartupTests
{
    private readonly CallLog _log = new();
    private readonly FakeDatabaseMaintenance _db;
    private readonly FakeBackupService _backups;
    private readonly FakeClock _clock = new();

    public DatabaseStartupTests()
    {
        _db = new FakeDatabaseMaintenance(_log);
        _backups = new FakeBackupService(_log);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DatabaseStartup CreateStartup(TimeSpan? closeTimeout = null) =>
        new(_db, _backups, _clock, NullLogger<DatabaseStartup>.Instance, closeTimeout ?? TimeSpan.FromSeconds(15));

    [Fact]
    public async Task BaseInexistente_SeCreaMigrandoYQuedaLista()
    {
        _db.Exists = false;

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal(["Exists", "Migrate", "EnableWal", "Backup:Automatic"], _log.Calls);
    }

    [Fact]
    public async Task BaseDaniada_DevuelveCorruptedConElRespaldoAutomaticoMasReciente()
    {
        var latest = new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow.AddHours(-2));
        _backups.Latest[BackupKind.Automatic] = latest;
        _db.Integrity = IntegrityState.Corrupted;

        var result = await CreateStartup().RunAsync(Ct);

        Assert.Equal(new StartupResult.Corrupted(latest), result);
        Assert.DoesNotContain("Migrate", _log.Calls);
    }

    [Fact]
    public async Task BaseDaniadaSinRespaldo_DevuelveCorruptedSinRespaldo()
    {
        _db.Integrity = IntegrityState.Corrupted;

        var result = await CreateStartup().RunAsync(Ct);

        Assert.Equal(new StartupResult.Corrupted(null), result);
    }

    [Theory]
    [InlineData(IntegrityState.Locked, DatabaseProblem.Locked)]
    [InlineData(IntegrityState.PermissionDenied, DatabaseProblem.PermissionDenied)]
    public async Task BaseInaccesible_DevuelveInaccessibleSinEscribir(IntegrityState state, DatabaseProblem reason)
    {
        _db.Integrity = state;

        var result = await CreateStartup().RunAsync(Ct);

        Assert.Equal(new StartupResult.Inaccessible(reason), result);
        Assert.Equal(["Exists", "CheckIntegrity"], _log.Calls);
    }

    [Fact]
    public async Task BaseMasNueva_DevuelveNewerDatabaseSinNingunaEscritura()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Newer, ["99990101000000_Future"]);

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.NewerDatabase>(result);
        Assert.Equal(["Exists", "CheckIntegrity", "GetMigrationState"], _log.Calls);
    }

    [Fact]
    public async Task MigracionPendiente_RespaldaAntesDeMigrar()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal(
            ["Exists", "CheckIntegrity", "GetMigrationState", "Backup:PreMigration", "Migrate", "EnableWal", "Backup:Automatic"],
            _log.Calls);
    }

    [Fact]
    public async Task MigracionPendienteSinEspacio_DevuelveInsufficientSpaceSinRespaldarNiMigrar()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);
        _db.SizeBytes = 1_000;
        _db.FreeSpaceBytes = 1_999;

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.InsufficientSpace>(result);
        Assert.DoesNotContain("Backup:PreMigration", _log.Calls);
        Assert.DoesNotContain("Migrate", _log.Calls);
    }

    [Fact]
    public async Task DiscoLlenoDuranteElRespaldoPrevio_DevuelveInsufficientSpaceSinMigrar()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);
        _backups.CreateExceptions[BackupKind.PreMigration] = new DatabaseAccessException(DatabaseProblem.DiskFull);

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.InsufficientSpace>(result);
        Assert.DoesNotContain("Migrate", _log.Calls);
    }

    [Fact]
    public async Task FallaDeMigracion_RestauraElRespaldoPrevioYDevuelveMigrationFailed()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);
        _db.MigrateException = new InvalidOperationException("falla simulada");

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.MigrationFailed>(result);
        Assert.Equal(
            ["Exists", "CheckIntegrity", "GetMigrationState", "Backup:PreMigration", "Migrate", "Restore:PreMigration"],
            _log.Calls);
        Assert.Equal(BackupKind.PreMigration, Assert.Single(_backups.Restored).Kind);
    }

    [Fact]
    public async Task SinPermisosAlCrearLaBase_DevuelveInaccessible()
    {
        _db.Exists = false;
        _db.MigrateException = new DatabaseAccessException(DatabaseProblem.PermissionDenied);

        var result = await CreateStartup().RunAsync(Ct);

        Assert.Equal(new StartupResult.Inaccessible(DatabaseProblem.PermissionDenied), result);
    }

    [Fact]
    public async Task RespaldoAutomaticoReciente_NoSeRepite()
    {
        _backups.Latest[BackupKind.Automatic] =
            new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow.AddHours(-23));

        await CreateStartup().RunAsync(Ct);

        Assert.DoesNotContain("Backup:Automatic", _log.Calls);
    }

    [Fact]
    public async Task RespaldoAutomaticoDeMasDe24Horas_SeCrea()
    {
        _backups.Latest[BackupKind.Automatic] =
            new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow.AddHours(-25));

        await CreateStartup().RunAsync(Ct);

        Assert.Contains("Backup:Automatic", _log.Calls);
    }

    [Fact]
    public async Task FallaDelRespaldoAutomatico_NoBloqueaElArranque()
    {
        _backups.CreateExceptions[BackupKind.Automatic] = new IOException("sin espacio");

        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
    }

    [Fact]
    public async Task RecoverFromBackup_ApartaLaBaseDaniadaYRestauraElRespaldo()
    {
        var backup = new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow.AddHours(-2));

        await CreateStartup().RecoverFromBackupAsync(backup, Ct);

        Assert.Equal(["Quarantine", "Restore:Automatic"], _log.Calls);
    }

    [Fact]
    public async Task BackupOnClose_RespetaElLimiteDeTiempoYNuncaLanza()
    {
        _backups.CreateDelay = TimeSpan.FromSeconds(5);
        var startup = CreateStartup(closeTimeout: TimeSpan.FromMilliseconds(100));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await startup.BackupOnCloseAsync();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Tardó {watch.Elapsed}");
    }

    [Fact]
    public async Task BackupOnClose_ConFalla_NoLanza()
    {
        _backups.CreateExceptions[BackupKind.Automatic] = new IOException("falla");

        await CreateStartup().BackupOnCloseAsync();

        Assert.Contains("Backup:Automatic", _log.Calls);
    }
}
