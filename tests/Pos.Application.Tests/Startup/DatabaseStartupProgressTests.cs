using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Startup;
using Pos.Application.Tests.TestSupport;

namespace Pos.Application.Tests.Startup;

/// <summary>Pasos que informa el arranque para la pantalla de carga (contracts/splash.md).</summary>
public class DatabaseStartupProgressTests
{
    private readonly CallLog _log = new();
    private readonly FakeDatabaseMaintenance _db;
    private readonly FakeBackupService _backups;
    private readonly FakeClock _clock = new();
    private readonly RecordingProgress _progress = new();

    public DatabaseStartupProgressTests()
    {
        _db = new FakeDatabaseMaintenance(_log);
        _backups = new FakeBackupService(_log);
        _backups.Latest[BackupKind.Automatic] = new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow.AddHours(-1));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DatabaseStartup CreateStartup() =>
        new(_db, _backups, _clock, NullLogger<DatabaseStartup>.Instance);

    [Fact]
    public async Task BaseAlDia_VerificaYPrepara()
    {
        await CreateStartup().RunAsync(Ct, _progress);

        Assert.Equal([StartupStep.CheckingDatabase, StartupStep.Finishing], _progress.Steps);
    }

    [Fact]
    public async Task RespaldoAutomaticoPendiente_SeInformaComoRespaldo()
    {
        _backups.Latest[BackupKind.Automatic] = null;

        await CreateStartup().RunAsync(Ct, _progress);

        Assert.Equal([StartupStep.CheckingDatabase, StartupStep.BackingUp, StartupStep.Finishing], _progress.Steps);
    }

    [Fact]
    public async Task MigracionPendiente_RespaldaYActualiza()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);

        await CreateStartup().RunAsync(Ct, _progress);

        Assert.Equal(
            [StartupStep.CheckingDatabase, StartupStep.BackingUp, StartupStep.Migrating, StartupStep.Finishing],
            _progress.Steps);
    }

    [Fact]
    public async Task BaseNueva_SeInformaComoActualizacion()
    {
        _db.Exists = false;

        await CreateStartup().RunAsync(Ct, _progress);

        Assert.Equal([StartupStep.CheckingDatabase, StartupStep.Migrating, StartupStep.Finishing], _progress.Steps);
    }

    [Fact]
    public async Task FallaDeMigracion_InformaLaRestauracion()
    {
        _db.Migrations = new MigrationState(MigrationStatus.Pending, ["002_Nueva"]);
        _db.MigrateException = new InvalidOperationException("falla");

        await CreateStartup().RunAsync(Ct, _progress);

        Assert.Equal(
            [StartupStep.CheckingDatabase, StartupStep.BackingUp, StartupStep.Migrating, StartupStep.Restoring],
            _progress.Steps);
    }

    [Fact]
    public async Task RecuperarDesdeRespaldo_InformaLaRestauracion()
    {
        var backup = new BackupInfo("/b/auto.db", BackupKind.Automatic, _clock.UtcNow);

        await CreateStartup().RecoverFromBackupAsync(backup, Ct, _progress);

        Assert.Equal([StartupStep.Restoring], _progress.Steps);
    }

    [Fact]
    public async Task SinProgreso_FuncionaIgual()
    {
        var result = await CreateStartup().RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
    }

    private sealed class RecordingProgress : IProgress<StartupStep>
    {
        public List<StartupStep> Steps { get; } = [];

        public void Report(StartupStep value) => Steps.Add(value);
    }
}
