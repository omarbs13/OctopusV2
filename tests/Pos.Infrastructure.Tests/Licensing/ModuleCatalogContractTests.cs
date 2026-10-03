using Pos.Domain.Licensing;
using Pos.Infrastructure.Licensing;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 025, FR-003 a FR-006, SC-001: el catálogo interno coincide exactamente con <c>contracts/module-catalog.json</c>
/// (el recurso embebido, idéntico al de OctopusAdmin) y los 6 identificadores históricos no cambian.
/// </summary>
public sealed class ModuleCatalogContractTests
{
    [Fact]
    public void ElCatalogoEmbebido_CoincideConLasConstantesDeLaAplicacion()
    {
        var contract = EmbeddedModuleCatalogInfo.Read();

        Assert.Equal(9, contract.Modules.Count);
        Assert.Equal(ModuleCatalog.All.Count, contract.Modules.Count);
        var ordered = contract.Modules.OrderBy(m => m.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var module = ModuleCatalog.All[i];
            Assert.Equal(ModuleCatalog.IdOf(module), ordered[i].Id);
            Assert.Equal(ModuleCatalog.KeyOf(module), ordered[i].Key);
            Assert.Equal(ModuleCatalog.OrderOf(module), ordered[i].Order);
            Assert.Equal(module == ModuleCatalog.Base, ordered[i].IsBase);
            Assert.False(string.IsNullOrWhiteSpace(ordered[i].Name));
        }

        Assert.Equal(LicensedModule.Pos, ModuleCatalog.Base);
        Assert.Single(contract.Modules, m => m.IsBase);
        Assert.True(contract.CatalogVersion >= 1);
    }

    [Theory]
    [InlineData(LicensedModule.Inventory, "7a99f06e-6c58-43fb-bc34-20bec30f8060")]
    [InlineData(LicensedModule.AdvancedReports, "d6c0dca2-2400-481b-94fc-2d90688eedfc")]
    [InlineData(LicensedModule.CreditAndCustomers, "be39a38f-0371-4b15-9bb6-fcb9c1bb1b7a")]
    [InlineData(LicensedModule.CashShifts, "b2145dea-fd36-48bc-85ba-773875b4a298")]
    [InlineData(LicensedModule.Returns, "2b1ab797-3339-43ef-a135-022f998177ca")]
    [InlineData(LicensedModule.Discounts, "01a0f957-082e-72cb-9c5a-9cc8fbee6772")]
    public void LosIdentificadoresHistoricos_NoCambian(LicensedModule module, string id) =>
        Assert.Equal(new Guid(id), ModuleCatalog.IdOf(module));

    [Fact]
    public void NombresYVersion_SeLeenDelRecursoEmbebido()
    {
        var info = new EmbeddedModuleCatalogInfo();

        Assert.Equal("Punto de venta", info.NameOf(LicensedModule.Pos));
        Assert.Equal("Categorías de productos", info.NameOf(LicensedModule.Categories));
        Assert.Equal(1, info.CatalogVersion);
    }
}
