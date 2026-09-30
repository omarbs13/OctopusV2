using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV y research §15: la migración <c>CashShifts</c> crea las dos tablas nuevas y agrega una
/// columna nula a <c>Sales</c>; no reconstruye ninguna tabla que ya tiene datos de clientes.
/// </summary>
public sealed class CashShiftsMigrationTests
{
    [Fact]
    public async Task CashShifts_NoReconstruyeTablasExistentes()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var cashShifts = migrations.Single(m => m.EndsWith("_CashShifts", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(cashShifts) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, cashShifts);

        foreach (var table in new[] { "CashShifts", "CashMovements" })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("ALTER TABLE \"Sales\" ADD \"CashShiftId\" TEXT NULL", script, StringComparison.Ordinal);
        Assert.Contains("WHERE \"Status\" = 'OPEN'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PRAGMA foreign_keys = 0", script, StringComparison.Ordinal);
    }
}
