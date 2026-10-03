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

    [Fact]
    public void LicenciaDeDomain_NoLeeJsonNiUsaCriptografia()
    {
        // 025, Principio II: el evaluador es puro; el JSON del catálogo y la firma viven en Infrastructure.
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace("Pos.Domain.Licensing")
            .ShouldNot()
            .HaveDependencyOnAny("System.Text.Json", "System.Security.Cryptography")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Licencia_FuncionaSinConexion()
    {
        // 025, FR-042: el control de licencias nunca abre conexiones de red. Leer la MAC
        // (System.Net.NetworkInformation) para el ID de máquina es local y sí se permite.
        foreach (var (assembly, ns) in new[]
        {
            (ApplicationAssembly, "Pos.Application.Licensing"),
            (InfrastructureAssembly, "Pos.Infrastructure.Licensing"),
        })
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespace(ns)
                .ShouldNot()
                .HaveDependencyOnAny("System.Net.Http", "System.Net.Sockets")
                .GetResult();

            AssertSuccess(result);
        }
    }

    private static void AssertSuccess(NetArchTest.Rules.TestResult result)
    {
        var failing = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);
        Assert.True(result.IsSuccessful, $"Tipos que violan la regla: {failing}");
    }
}
