using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV: la migración <c>ReportsAnalytics</c> agrega una columna y un índice; no reconstruye
/// <c>Products</c> ni ninguna otra tabla con datos de clientes.
/// </summary>
public sealed class ReportsAnalyticsMigrationTests
{
    [Fact]
    public async Task ReportsAnalytics_NoReconstruyeTablasExistentes()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var reports = migrations.Single(m => m.EndsWith("_ReportsAnalytics", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(reports) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, reports);

        Assert.Contains("ALTER TABLE \"Products\" ADD \"IsCritical\" INTEGER NOT NULL DEFAULT 0", script, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX \"IX_InventoryMovements_Product_CreatedAt\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }
}
