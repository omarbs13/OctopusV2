using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (015): la migración <c>DiscountsAndCoupons</c> crea 3 tablas, agrega 4 columnas con
/// <c>ADD COLUMN</c> y copia <c>AmountCents</c> en <c>OriginalAmountCents</c>; no reconstruye ninguna tabla.
/// </summary>
public sealed class DiscountsMigrationTests
{
    [Fact]
    public async Task DiscountsAndCoupons_SoloCreaTablasAgregaColumnasYCopiaElImporteOriginal()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var discounts = migrations.Single(m => m.EndsWith("_DiscountsAndCoupons", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(discounts) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, discounts);

        foreach (var table in new[] { "Coupons", "DiscountApprovals", "SaleDiscounts" })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("ALTER TABLE \"Sales\" ADD \"DiscountCents\" INTEGER NOT NULL DEFAULT 0", script, StringComparison.Ordinal);
        foreach (var column in new[] { "OriginalAmountCents", "LineDiscountCents", "OrderDiscountCents" })
        {
            Assert.Contains($"ALTER TABLE \"SaleLines\" ADD \"{column}\" INTEGER NOT NULL DEFAULT 0", script, StringComparison.Ordinal);
        }

        Assert.Equal(4, script.Split('\n').Count(l => l.StartsWith("ALTER TABLE", StringComparison.Ordinal)));
        Assert.Contains("UPDATE \"SaleLines\" SET \"OriginalAmountCents\" = \"AmountCents\";", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Coupons_Code\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscountsAndCoupons_EsLaUltimaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();

        Assert.EndsWith("_DiscountsAndCoupons", context.Database.GetMigrations().Last(), StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VentasAnterioresA0100_QuedanConImporteOriginalIgualAlRegistradoYSinDescuentos()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.9.0.db"), dir.Paths.DatabaseFile);
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        using var connection = new SqliteConnection($"Data Source={dir.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SaleLines";
        Assert.True((long)command.ExecuteScalar()! > 0);
        command.CommandText = "SELECT COUNT(*) FROM SaleLines WHERE OriginalAmountCents <> AmountCents OR LineDiscountCents <> 0 OR OrderDiscountCents <> 0";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT COUNT(*) FROM Sales WHERE DiscountCents <> 0";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT (SELECT COUNT(*) FROM Coupons) + (SELECT COUNT(*) FROM SaleDiscounts) + (SELECT COUNT(*) FROM DiscountApprovals)";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }
}
