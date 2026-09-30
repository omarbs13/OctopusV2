using System.Xml.Linq;

namespace Pos.ArchitectureTests;

/// <summary>
/// Verifica las referencias directas entre proyectos declaradas en los .csproj.
/// </summary>
public class ProjectReferenceTests
{
    public static TheoryData<string, string[]> AllowedReferences => new()
    {
        { "Pos.Domain", [] },
        { "Pos.Application", ["Pos.Domain"] },
        { "Pos.Infrastructure", ["Pos.Application", "Pos.Domain"] },
        { "Pos.Desktop", ["Pos.Application", "Pos.Infrastructure"] },
    };

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void Proyecto_SoloReferenciaLosProyectosPermitidos(string project, string[] allowed)
    {
        var csproj = Path.Combine(FindRepositoryRoot(), "src", project, $"{project}.csproj");

        var references = XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(
                e.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(allowed.Order(StringComparer.Ordinal), references);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pos.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Pos.slnx en los directorios superiores.");
    }
}
