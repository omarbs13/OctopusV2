namespace Pos.ArchitectureTests;

/// <summary>
/// 018 (research §11): la bitácora nunca se modifica ni se borra. <c>ExecuteUpdate</c> y
/// <c>ExecuteDelete</c> saltan el <c>ChangeTracker</c> y con él a <c>RejectImmutableChanges</c>, así que
/// ningún archivo de Infrastructure que toque la bitácora puede usarlos. NetArchTest no ve sobre qué
/// <c>DbSet</c> se hace una llamada, por eso la regla revisa el código fuente.
/// </summary>
public class AuditImmutabilityTests
{
    private static readonly string[] AuditNames = ["AuditEntries", "AuditEntry"];
    private static readonly string[] BulkOperations = ["ExecuteUpdate", "ExecuteDelete"];

    [Fact]
    public void Infrastructure_NoUsaExecuteUpdateNiExecuteDeleteSobreLaBitacora()
    {
        var infrastructure = Path.Combine(FindRepositoryRoot(), "src", "Pos.Infrastructure");
        var migrations = Path.Combine(infrastructure, "Persistence", "Migrations") + Path.DirectorySeparatorChar;

        var offenders = Directory.EnumerateFiles(infrastructure, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(migrations, StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f =>
            {
                var code = File.ReadAllText(f);
                return AuditNames.Any(n => code.Contains(n, StringComparison.Ordinal))
                    && BulkOperations.Any(o => code.Contains(o, StringComparison.Ordinal));
            })
            .Select(f => Path.GetRelativePath(infrastructure, f))
            .ToList();

        Assert.True(offenders.Count == 0, $"Usan ExecuteUpdate o ExecuteDelete junto a la bitácora: {string.Join(", ", offenders)}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pos.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró Pos.slnx.");
    }
}
