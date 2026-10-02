using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (021, research §3): la migración <c>ScannerBarcodeFormats</c> solo actualiza el snapshot
/// (<c>HasMaxLength</c> 14 → 48); SQLite no guarda el largo de <c>TEXT</c>, así que no genera SQL ni
/// reconstruye <c>Products</c>.
/// </summary>
public sealed class ScannerBarcodeFormatsMigrationTests
{
    [Fact]
    public async Task ScannerBarcodeFormats_SoloRegistraLaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var migration = migrations.Single(m => m.EndsWith("_ScannerBarcodeFormats", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(migration) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, migration);
        var statements = script.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && l is not ("BEGIN TRANSACTION;" or "COMMIT;"))
            .ToList();

        Assert.EndsWith("_SuppliersAndPurchases", previous, StringComparison.Ordinal);
        Assert.Equal("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")", statements[0]);
        Assert.Equal(2, statements.Count);
    }
}
