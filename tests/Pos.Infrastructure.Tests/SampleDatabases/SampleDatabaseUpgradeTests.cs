using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Startup;
using Pos.Infrastructure.Startup;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Toda base de ejemplo de una versión publicada migra a la versión actual sin perder datos
/// (constitución, Principio IV; SC-007).
/// </summary>
public sealed class SampleDatabaseUpgradeTests
{
    public static TheoryData<string> SampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SampleDatabases"), "v*.db").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public async Task BaseDeEjemplo_MigraALaVersionActualConservandoLosDatos(string sampleFile)
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", sampleFile), dir.Paths.DatabaseFile);
        using var db = TestDb.CreateUnmigrated(dir);
        var backups = new SqliteBackupService(dir.Paths, db.Clock, NullLogger<SqliteBackupService>.Instance);
        var startup = new DatabaseStartup(
            new SqliteDatabaseMaintenance(db, dir.Paths),
            backups,
            db.Clock,
            NullLogger<DatabaseStartup>.Instance);

        var result = await startup.RunAsync(TestContext.Current.CancellationToken);

        Assert.IsType<StartupResult.Ready>(result);
        Assert.Equal("ok", DatabaseTestHelpers.QuickCheck(dir.Paths.DatabaseFile));

        using var connection = new SqliteConnection($"Data Source={dir.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        Assert.Equal(SampleData.ProductCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Products"));
        Assert.Equal(1, Scalar<long>(connection, $"SELECT COUNT(*) FROM Products WHERE Sku = '{SampleData.DeletedSku}' AND DeletedAt IS NOT NULL"));
        Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE IsActive = 0 AND DeletedAt IS NULL"));
        Assert.Equal(SampleData.AccentedName, Scalar<string>(connection, $"SELECT Name FROM Products WHERE Sku = '{SampleData.AccentedSku}'"));
        Assert.Equal(SampleData.AccentedPriceCents, Scalar<long>(connection, $"SELECT PriceCents FROM Products WHERE Sku = '{SampleData.AccentedSku}'"));
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
