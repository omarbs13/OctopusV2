using System.Reflection;
using NetArchTest.Rules;

namespace Pos.ArchitectureTests;

/// <summary>
/// Reglas de dependencia entre capas (constitución, Principio II).
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("Pos.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Pos.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("Pos.Infrastructure");
    private static readonly Assembly DesktopAssembly = Assembly.Load("Pos");

    [Fact]
    public void Domain_NoDependeDeOtrasCapasNiDeInfraestructura()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Pos.Application",
                "Pos.Infrastructure",
                "Pos.Desktop",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.Sqlite",
                "Avalonia",
                "Serilog")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Application_NoDependeDeInfrastructureDesktopEfCoreNiAvalonia()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Pos.Infrastructure",
                "Pos.Desktop",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.Sqlite",
                "Avalonia",
                "Serilog")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Infrastructure_NoDependeDeDesktop()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Pos.Desktop", "Avalonia")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Desktop_FueraDeComposition_NoAccedeAInfrastructureNiABaseDeDatos()
    {
        var result = Types.InAssembly(DesktopAssembly)
            .That()
            .DoNotResideInNamespace("Pos.Desktop.Composition")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Pos.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.Sqlite")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void ViewModelsYVistas_NoAccedenALaBaseDeDatos()
    {
        var result = Types.InAssembly(DesktopAssembly)
            .That()
            .HaveNameEndingWith("ViewModel", StringComparison.Ordinal)
            .Or()
            .HaveNameEndingWith("View", StringComparison.Ordinal)
            .Or()
            .HaveNameEndingWith("Window", StringComparison.Ordinal)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Pos.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.Sqlite")
            .GetResult();

        AssertSuccess(result);
    }

    private static void AssertSuccess(NetArchTest.Rules.TestResult result)
    {
        var failing = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);
        Assert.True(result.IsSuccessful, $"Tipos que violan la regla: {failing}");
    }
}
