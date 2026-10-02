using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (020, research §12): la migración <c>SuppliersAndPurchases</c> solo crea las tablas
/// <c>Suppliers</c>, <c>Purchases</c> y <c>PurchaseLines</c> con sus índices, sin tocar tablas existentes:
/// los movimientos de inventario y las existencias se conservan intactos.
/// </summary>
public sealed class SuppliersAndPurchasesMigrationTests
{
    [Fact]
    public async Task SuppliersAndPurchases_SoloCreaTresTablasYSusIndices()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var migration = migrations.Single(m => m.EndsWith("_SuppliersAndPurchases", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(migration) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, migration);
        var lines = script.Split('\n');

        Assert.Equal(
            ["CREATE TABLE \"Suppliers\" (", "CREATE TABLE \"Purchases\" (", "CREATE TABLE \"PurchaseLines\" ("],
            lines.Where(l => l.StartsWith("CREATE TABLE", StringComparison.Ordinal)).Select(l => l.TrimEnd('\r')));
        Assert.Equal(10, lines.Count(l => l.StartsWith("CREATE INDEX", StringComparison.Ordinal) || l.StartsWith("CREATE UNIQUE INDEX", StringComparison.Ordinal)));
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
        Assert.Equal(1, lines.Count(l => l.StartsWith("INSERT INTO", StringComparison.Ordinal)));
        Assert.Contains("INSERT INTO \"__EFMigrationsHistory\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrarDesde0130_ConservaMovimientosYExistencias()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.13.0.db"), dir.Paths.DatabaseFile);
        const string Movements = "SELECT Id, ProductId, Sequence, Type, Quantity, ResultingStock FROM InventoryMovements ORDER BY Id";
        const string Stocks = "SELECT ProductId, OnHand, MovementCount, Version FROM ProductStocks ORDER BY ProductId";
        var movementsBefore = Read(dir.Paths.DatabaseFile, Movements);
        var stocksBefore = Read(dir.Paths.DatabaseFile, Stocks);
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(movementsBefore);
        Assert.Equal(movementsBefore, Read(dir.Paths.DatabaseFile, Movements));
        Assert.Equal(stocksBefore, Read(dir.Paths.DatabaseFile, Stocks));
        foreach (var table in new[] { "Suppliers", "Purchases", "PurchaseLines" })
        {
            Assert.Equal(["0"], Read(dir.Paths.DatabaseFile, $"SELECT COUNT(*) FROM {table}"));
        }

        Assert.Contains(
            "WHERE \"Status\" = 'ACTIVE'",
            Read(dir.Paths.DatabaseFile, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'IX_Purchases_Supplier_InvoiceKey'").Single(),
            StringComparison.Ordinal);
    }

    private static List<string> Read(string file, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={file};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "∅" : reader.GetValue(i).ToString())));
        }

        return rows;
    }
}
