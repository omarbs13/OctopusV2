using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (013): la migración <c>ReturnsAndCreditNotes</c> solo crea tablas y agrega columnas con
/// valor por defecto o nulas; no reconstruye ninguna tabla con datos.
/// </summary>
public sealed class ReturnsMigrationTests
{
    [Fact]
    public async Task ReturnsAndCreditNotes_SoloCreaTablasYAgregaColumnasSinReconstruirNada()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var returns = migrations.Single(m => m.EndsWith("_ReturnsAndCreditNotes", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(returns) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, returns);

        foreach (var table in new[] { "SaleReturns", "SaleReturnLines", "SaleReturnRefunds", "CreditNotes", "CreditNoteMovements" })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("ALTER TABLE \"Sales\" ADD \"ReturnedCents\" INTEGER NOT NULL DEFAULT 0", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE \"SaleLines\" ADD \"ReturnedQuantity\" INTEGER NOT NULL DEFAULT 0", script, StringComparison.Ordinal);
        Assert.Equal(6, script.Split('\n').Count(l => l.StartsWith("ALTER TABLE", StringComparison.Ordinal) && l.Contains(" ADD ", StringComparison.Ordinal)));
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }
}
