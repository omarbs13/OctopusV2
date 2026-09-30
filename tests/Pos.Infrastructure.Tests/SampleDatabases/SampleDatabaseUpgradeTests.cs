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

        // 003: unidad de medida (clarificación 1) e índices filtrados tras reconstruir Products.
        Assert.Equal(8, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure"));
        Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE UnitCode IS NULL OR UnitCode NOT IN (SELECT Code FROM UnitsOfMeasure)"));
        if (sampleFile == "v0.1.0.db")
        {
            Assert.Equal(SampleData.ProductCount, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE UnitCode = 'H87'"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductImages"));
        }
        else
        {
            // Desde 0.2.0: unidad, imagen y precio 0 se conservan al migrar.
            Assert.Equal("KGM", Scalar<string>(connection, $"SELECT UnitCode FROM Products WHERE Sku = '{SampleData.KilogramSku}'"));
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductImages"));
            Assert.Equal(
                [SampleData.ImageSeed, SampleData.ImageSeed, SampleData.ImageSeed],
                Scalar<byte[]>(connection, $"SELECT i.Content FROM ProductImages i JOIN Products p ON p.Id = i.ProductId WHERE p.Sku = '{SampleData.ImageSku}'"));
            Assert.Equal(0, Scalar<long>(connection, $"SELECT PriceCents FROM Products WHERE Sku = '{SampleData.ZeroPriceSku}'"));
        }

        AssertInventory(connection, sampleFile);

        foreach (var index in new[] { "IX_Products_Sku", "IX_Products_Barcode", "IX_Products_NameSearch" })
        {
            var sql = Scalar<string>(connection, $"SELECT sql FROM sqlite_master WHERE type = 'index' AND name = '{index}'");
            Assert.Contains("WHERE \"DeletedAt\" IS NULL", sql, StringComparison.Ordinal);
        }

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check";
        using var violations = foreignKeys.ExecuteReader();
        Assert.False(violations.Read());
    }

    /// <summary>004: los productos existentes quedan sin control de inventario, sin existencia ni movimientos (FR-023).</summary>
    private static void AssertInventory(SqliteConnection connection, string sampleFile)
    {
        Assert.Equal(3, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure WHERE DecimalPlaces = 3 AND Code IN ('KGM', 'LTR', 'MTR')"));
        Assert.Equal(5, Scalar<long>(connection, "SELECT COUNT(*) FROM UnitsOfMeasure WHERE DecimalPlaces = 0"));

        if (sampleFile == "v0.3.0.db")
        {
            Assert.Equal(1, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE TracksInventory = 1"));
            Assert.Equal(5000, Scalar<long>(connection, $"SELECT MinimumStock FROM Products WHERE Sku = '{SampleData.InventorySku}'"));
            Assert.Equal(SampleData.InventoryOnHandThousandths, Scalar<long>(connection, "SELECT OnHand FROM ProductStocks"));
            Assert.Equal(SampleData.InventoryMovementCount, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements"));
            Assert.Equal(4, Scalar<long>(connection, "SELECT COUNT(DISTINCT Type) FROM InventoryMovements"));
        }
        else
        {
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM Products WHERE TracksInventory <> 0 OR MinimumStock IS NOT NULL"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM ProductStocks"));
            Assert.Equal(0, Scalar<long>(connection, "SELECT COUNT(*) FROM InventoryMovements"));
        }
    }

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }
}
