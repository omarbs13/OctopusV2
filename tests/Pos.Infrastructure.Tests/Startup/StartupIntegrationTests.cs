using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Startup;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Tests.TestSupport.MigrationScenarios;

namespace Pos.Infrastructure.Tests.Startup;

/// <summary>Los cinco escenarios de arranque con adaptadores reales (SC-006).</summary>
public sealed class StartupIntegrationTests : IDisposable
{
    private readonly TempDataDirectory _dir = new();
    private readonly TestClock _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _dir.Dispose();

    private (DatabaseStartup Startup, SqliteBackupService Backups) Create(IDbContextFactory<PosDbContext> factory)
    {
        var backups = new SqliteBackupService(_dir.Paths, _clock, NullLogger<SqliteBackupService>.Instance);
        var maintenance = new SqliteDatabaseMaintenance(factory, _dir.Paths);
        var startup = new DatabaseStartup(maintenance, backups, _clock, NullLogger<DatabaseStartup>.Instance);
        return (startup, backups);
    }

    [Fact]
    public async Task BaseNueva_SeCreaYQuedaLista()
    {
        using var db = TestDb.CreateUnmigrated(_dir);
        var (startup, _) = Create(db);

        var result = await startup.RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.True(File.Exists(_dir.Paths.DatabaseFile));
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(_dir.Paths.DatabaseFile));
    }

    [Fact]
    public async Task MigracionPendiente_RespaldaMigraYConservaLosDatos()
    {
        var factory = ScenarioContextFactory.Pending(_dir.Paths.DatabaseFile);
        await ScenarioMaintenance.MigrateToS1Async(factory);
        await DatabaseTestHelpers.SeedProductsAsync(factory, 3);
        var before = DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile);
        var (startup, _) = Create(factory);

        var result = await startup.RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Single(Directory.GetFiles(_dir.Paths.PreMigrationBackupsDirectory));
        Assert.Contains("Notes", DatabaseTestHelpers.ReadColumns(_dir.Paths.DatabaseFile, "Products"));
        Assert.Equal(before, DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile));
    }

    [Fact]
    public async Task BaseMasNueva_NoSeAbreNiSeModifica()
    {
        using var db = await TestDb.CreateAsync(_dir);
        DatabaseTestHelpers.Execute(
            _dir.Paths.DatabaseFile,
            "INSERT INTO __EFMigrationsHistory VALUES ('99990101000000_Future', '10.0.0');");
        var hashBefore = DatabaseTestHelpers.Hash(_dir.Paths.DatabaseFile);
        var (startup, _) = Create(db);

        var result = await startup.RunAsync(Ct);

        Assert.IsType<StartupResult.NewerDatabase>(result);
        Assert.Equal(hashBefore, DatabaseTestHelpers.Hash(_dir.Paths.DatabaseFile));
        Assert.Empty(Directory.GetFiles(_dir.Paths.PreMigrationBackupsDirectory));
    }

    [Fact]
    public async Task FallaDeMigracion_RestauraElRespaldoConLosDatosIdenticos()
    {
        var factory = ScenarioContextFactory.Failing(_dir.Paths.DatabaseFile);
        await ScenarioMaintenance.MigrateToS1Async(factory);
        await DatabaseTestHelpers.SeedProductsAsync(factory, 3);
        var before = DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile);
        var (startup, _) = Create(factory);

        var result = await startup.RunAsync(Ct);

        Assert.IsType<StartupResult.MigrationFailed>(result);
        Assert.DoesNotContain("Notes", DatabaseTestHelpers.ReadColumns(_dir.Paths.DatabaseFile, "Products"));
        Assert.Equal(before, DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile));
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(_dir.Paths.DatabaseFile));
    }

    [Fact]
    public async Task BaseDaniadaConRespaldo_SeRecuperaConLosDatosDelRespaldo()
    {
        using var db = await TestDb.CreateAsync(_dir);
        await DatabaseTestHelpers.SeedProductsAsync(db, 3);
        var (startup, backups) = Create(db);
        var backup = await backups.CreateAsync(BackupKind.Automatic, Ct);
        var expected = DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile);
        DatabaseTestHelpers.CorruptHeader(_dir.Paths.DatabaseFile);

        var corrupted = await startup.RunAsync(Ct);

        Assert.Equal(new StartupResult.Corrupted(backup), corrupted);

        await startup.RecoverFromBackupAsync(backup, Ct);
        var result = await startup.RunAsync(Ct);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal(expected, DatabaseTestHelpers.ReadProductRows(_dir.Paths.DatabaseFile));
        Assert.Single(Directory.GetDirectories(_dir.Paths.CorruptDirectory));
    }
}
