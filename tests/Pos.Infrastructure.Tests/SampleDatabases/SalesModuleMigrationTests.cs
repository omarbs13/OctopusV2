using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV y research §13: la migración <c>SalesModule</c> solo crea las tablas nuevas; no
/// reconstruye <c>Products</c>, <c>ProductStocks</c> ni <c>InventoryMovements</c>, que en las bases
/// de los clientes ya tienen datos.
/// </summary>
public sealed class SalesModuleMigrationTests
{
    [Fact]
    public async Task SalesModule_OnlyCreatesTheNewTablesAndIndexes()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var salesModule = migrations.Single(m => m.EndsWith("_SalesModule", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(salesModule) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, salesModule);

        foreach (var table in new[] { "Sales", "SaleLines", "SalePayments", "SaleDrafts", "AuditEntries" })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("DROP TABLE", script.Replace("DROP TABLE IF EXISTS", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PRAGMA foreign_keys = 0", script, StringComparison.Ordinal);
        foreach (var existing in new[] { "\"Products\"", "\"ProductStocks\"", "\"InventoryMovements\"" })
        {
            Assert.DoesNotContain($"CREATE TABLE {existing}", script, StringComparison.Ordinal);
            Assert.DoesNotContain($"CREATE TABLE \"ef_temp_{existing.Trim('"')}\"", script, StringComparison.Ordinal);
        }
    }
}
