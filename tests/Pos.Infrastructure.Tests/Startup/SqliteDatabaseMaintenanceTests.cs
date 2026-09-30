using Pos.Application.Startup;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Tests.TestSupport.MigrationScenarios;

namespace Pos.Infrastructure.Tests.Startup;

public sealed class SqliteDatabaseMaintenanceTests : IDisposable
{
    private readonly TempDataDirectory _dir = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _dir.Dispose();

    private async Task<(TestDb Db, SqliteDatabaseMaintenance Maintenance)> CreateMigratedAsync()
    {
        var db = await TestDb.CreateAsync(_dir);
        return (db, new SqliteDatabaseMaintenance(db, _dir.Paths));
    }

    [Fact]
    public async Task SinBase_DatabaseExistsEsFalso_YMigrarLaCrea()
    {
        using var db = TestDb.CreateUnmigrated(_dir);
        var maintenance = new SqliteDatabaseMaintenance(db, _dir.Paths);

        Assert.False(maintenance.DatabaseExists());

        await maintenance.MigrateAsync(Ct);

        Assert.True(maintenance.DatabaseExists());
        Assert.Equal(MigrationStatus.UpToDate, (await maintenance.GetMigrationStateAsync(Ct)).Status);
    }

    [Fact]
    public async Task BaseMigrada_EstaAlDiaEIntegra()
    {
        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;

        Assert.True(maintenance.DatabaseExists());
        Assert.Equal(IntegrityState.Ok, await maintenance.CheckIntegrityAsync(Ct));
        Assert.Equal(MigrationStatus.UpToDate, (await maintenance.GetMigrationStateAsync(Ct)).Status);
    }

    [Fact]
    public async Task MigracionDesconocidaEnElHistorial_EsNewer()
    {
        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;
        DatabaseTestHelpers.Execute(
            _dir.Paths.DatabaseFile,
            "INSERT INTO __EFMigrationsHistory VALUES ('99990101000000_Future', '10.0.0');");

        var state = await maintenance.GetMigrationStateAsync(Ct);

        Assert.Equal(MigrationStatus.Newer, state.Status);
        Assert.Equal(["99990101000000_Future"], state.Migrations);
    }

    [Fact]
    public async Task BaseConMigracionPendiente_EsPending()
    {
        var factory = ScenarioContextFactory.Pending(_dir.Paths.DatabaseFile);
        await ScenarioMaintenance.MigrateToS1Async(factory);
        var maintenance = ScenarioMaintenance.Create(_dir.Paths, factory);

        var state = await maintenance.GetMigrationStateAsync(Ct);

        Assert.Equal(MigrationStatus.Pending, state.Status);
        Assert.Equal(["00000000000002_S2_AddColumn"], state.Migrations);
    }

    [Fact]
    public async Task CabeceraDaniada_EsCorrupted()
    {
        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;
        DatabaseTestHelpers.CorruptHeader(_dir.Paths.DatabaseFile);

        Assert.Equal(IntegrityState.Corrupted, await maintenance.CheckIntegrityAsync(Ct));
    }

    [Fact]
    public async Task CarpetaSinPermisosDeEscritura_EsPermissionDenied()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Los permisos POSIX solo se simulan en Linux.");
        Assert.SkipWhen(Environment.UserName == "root", "root ignora los permisos de archivo.");

        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;
        var dataDir = Path.GetDirectoryName(_dir.Paths.DatabaseFile)!;
        UnixPermissions.MakeReadOnly(dataDir);
        try
        {
            Assert.Equal(IntegrityState.PermissionDenied, await maintenance.CheckIntegrityAsync(Ct));
        }
        finally
        {
            UnixPermissions.MakeWritable(dataDir);
        }
    }

    [Fact]
    public async Task EnableWal_DejaLaBaseEnModoWal()
    {
        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;

        await maintenance.EnableWalAsync(Ct);

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dir.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", command.ExecuteScalar());
    }

    [Fact]
    public async Task EspacioYTamanio_SonPositivos()
    {
        var (db, maintenance) = await CreateMigratedAsync();
        using var _ = db;

        Assert.True(maintenance.DatabaseSizeBytes() > 0);
        Assert.True(maintenance.AvailableFreeSpaceBytes() > 0);
    }
}
