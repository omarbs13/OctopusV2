using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (022, research §13): la migración <c>LowStockAlerts</c> agrega la columna nula
/// <c>Products.ReorderPoint</c> con <c>ALTER TABLE ... ADD</c>, sin reconstruir <c>Products</c>, y crea la
/// tabla <c>StockAlertAcknowledgements</c> con su índice único.
/// </summary>
public sealed class LowStockAlertsMigrationTests
{
    [Fact]
    public async Task LowStockAlerts_AgregaColumnaYTablaSinReconstruirProducts()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var migration = migrations.Single(m => m.EndsWith("_LowStockAlerts", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(migration) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, migration);
        var lines = script.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.EndsWith("_ScannerBarcodeFormats", previous, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE \"Products\" ADD \"ReorderPoint\" INTEGER NULL;", lines);
        Assert.Equal(
            ["CREATE TABLE \"StockAlertAcknowledgements\" ("],
            lines.Where(l => l.StartsWith("CREATE TABLE", StringComparison.Ordinal)));
        Assert.Single(lines, l => l.StartsWith(
            "CREATE UNIQUE INDEX \"IX_StockAlertAcknowledgements_User_Date_Level_Product\"", StringComparison.Ordinal));
        Assert.Equal(1, lines.Count(l => l.StartsWith("ALTER TABLE", StringComparison.Ordinal)));
        Assert.DoesNotContain("ef_temp_Products", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE \"Products\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LowStockAlerts_EsLaUltimaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();

        Assert.EndsWith("_LowStockAlerts", context.Database.GetMigrations().Last(), StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }
}
