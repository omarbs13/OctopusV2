using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Tests.TestSupport.MigrationScenarios;

/// <summary>
/// Contexto de prueba con migraciones escritas a mano en este ensamblado: S1_Initial y
/// S2_AddColumn. Sirve para simular una base con una migración pendiente.
/// </summary>
public sealed class ScenarioDbContext : PosDbContext
{
    public ScenarioDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public static DbContextOptions Options(string databaseFile) =>
        BuildOptions<ScenarioDbContext>(databaseFile);

    internal static DbContextOptions BuildOptions<T>(string databaseFile)
        where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlite(SqliteConnectionStrings.For(databaseFile))
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
}

/// <summary>Contexto de prueba con S1_Initial y S2_Failing, que falla al aplicarse.</summary>
public sealed class FailingScenarioDbContext : PosDbContext
{
    public FailingScenarioDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public static DbContextOptions Options(string databaseFile) =>
        ScenarioDbContext.BuildOptions<FailingScenarioDbContext>(databaseFile);
}

/// <summary>Fábrica que entrega el contexto de escenario como <see cref="PosDbContext"/>.</summary>
public sealed class ScenarioContextFactory : IDbContextFactory<PosDbContext>
{
    private readonly Func<PosDbContext> _create;

    private ScenarioContextFactory(Func<PosDbContext> create) => _create = create;

    public static ScenarioContextFactory Pending(string databaseFile) =>
        new(() => new ScenarioDbContext(ScenarioDbContext.Options(databaseFile)));

    public static ScenarioContextFactory Failing(string databaseFile) =>
        new(() => new FailingScenarioDbContext(FailingScenarioDbContext.Options(databaseFile)));

    public PosDbContext CreateDbContext() => _create();
}
