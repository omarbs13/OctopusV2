using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (014): la migración <c>CustomersAndCredit</c> solo crea las 4 tablas nuevas y agrega 5
/// columnas nulas a <c>CashShifts</c>; no reconstruye ninguna tabla ni rellena datos.
/// </summary>
public sealed class CustomersMigrationTests
{
    private static readonly string[] CashShiftColumns =
    [
        "OnAccountSalesCents",
        "CustomerPaymentsCashCents",
        "CustomerPaymentsNonCashCents",
        "CustomerPaymentVoidsCashCents",
        "CustomerPaymentVoidsNonCashCents",
    ];

    [Fact]
    public async Task CustomersAndCredit_SoloCreaTablasYAgregaColumnasNulasSinReconstruirNada()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var credit = migrations.Single(m => m.EndsWith("_CustomersAndCredit", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(credit) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, credit);

        foreach (var table in new[] { "Customers", "Receivables", "ReceivableEntries", "CustomerPayments" })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", script, StringComparison.Ordinal);
        }

        foreach (var column in CashShiftColumns)
        {
            Assert.Contains($"ALTER TABLE \"CashShifts\" ADD \"{column}\" INTEGER NULL", script, StringComparison.Ordinal);
        }

        Assert.Equal(5, script.Split('\n').Count(l => l.StartsWith("ALTER TABLE", StringComparison.Ordinal)));
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Customers_TaxId\" ON \"Customers\" (\"TaxId\") WHERE \"TaxId\" IS NOT NULL", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO \"Customers\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomersAndCredit_EsLaUltimaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();

        Assert.EndsWith("_CustomersAndCredit", context.Database.GetMigrations().Last(), StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TurnosCerradosAntesDe090_QuedanConElBloqueCreditoEnNulo()
    {
        using var dir = new TempDataDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "SampleDatabases", "v0.8.0.db"), dir.Paths.DatabaseFile);
        using (var db = TestDb.CreateUnmigrated(dir))
        await using (var context = db.CreateDbContext())
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        using var connection = new SqliteConnection($"Data Source={dir.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM CashShifts WHERE Status = 'CLOSED' AND ({string.Join(" OR ", CashShiftColumns.Select(c => $"{c} IS NOT NULL"))})";
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT COUNT(*) FROM CashShifts WHERE Status = 'CLOSED'";
        Assert.True((long)command.ExecuteScalar()! > 0);
    }
}
