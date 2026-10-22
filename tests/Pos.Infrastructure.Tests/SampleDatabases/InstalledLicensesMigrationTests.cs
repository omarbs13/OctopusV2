using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>
/// Principio IV (025): la migración <c>InstalledLicenses</c> solo crea la tabla de la licencia importada y no
/// reconstruye tablas con datos. La migración de las bases de ejemplo la cubre <see cref="SampleDatabaseUpgradeTests"/>.
/// </summary>
public sealed class InstalledLicensesMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InstalledLicenses_SoloCreaLaTabla_YQuedaVacia()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var installed = migrations.Single(m => m.EndsWith("_InstalledLicenses", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(installed) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, installed);

        Assert.Contains("CREATE TABLE \"InstalledLicenses\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
        Assert.Equal(0, await context.InstalledLicenses.CountAsync(Ct));
    }

    [Fact]
    public async Task InstalledLicenses_EsLaUltimaMigracion()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();

        Assert.EndsWith("_InstalledLicenses", context.Database.GetMigrations().Last(), StringComparison.Ordinal);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
    }
}
