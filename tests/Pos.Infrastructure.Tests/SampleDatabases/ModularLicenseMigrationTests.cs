using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.SampleDatabases;

/// <summary>Principio IV: la migración <c>ModularLicense</c> solo crea <c>LicenseSeals</c>; no reconstruye tablas con datos.</summary>
public sealed class ModularLicenseMigrationTests
{
    [Fact]
    public async Task ModularLicense_SoloCreaLaTablaDelSello()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToList();
        var modular = migrations.Single(m => m.EndsWith("_ModularLicense", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(modular) - 1];

        var script = context.GetService<IMigrator>().GenerateScript(previous, modular);

        Assert.Contains("CREATE TABLE \"LicenseSeals\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ef_temp_", script, StringComparison.Ordinal);
    }
}
