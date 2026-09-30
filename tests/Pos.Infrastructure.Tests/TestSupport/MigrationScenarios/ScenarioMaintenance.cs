using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Startup;

namespace Pos.Infrastructure.Tests.TestSupport.MigrationScenarios;

public static class ScenarioMaintenance
{
    public const string S1 = "00000000000001_S1_Initial";

    /// <summary>Mantenimiento de base que usa las migraciones del contexto de escenario.</summary>
    public static SqliteDatabaseMaintenance Create(AppPaths paths, IDbContextFactory<PosDbContext> factory) =>
        new(factory, paths);

    /// <summary>Crea la base aplicando solo S1_Initial.</summary>
    public static async Task MigrateToS1Async(IDbContextFactory<PosDbContext> factory)
    {
        await using var context = factory.CreateDbContext();
        await context.GetService<IMigrator>().MigrateAsync(S1);
    }
}
