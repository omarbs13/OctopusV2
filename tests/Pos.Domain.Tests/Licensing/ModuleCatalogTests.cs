using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>012, FR-007 y FR-008; 025, FR-001/FR-002: 9 módulos con identificadores únicos, POS como base.</summary>
public sealed class ModuleCatalogTests
{
    [Fact]
    public void CadaModuloTieneUnIdentificadorUnico_QueSeResuelveDeVuelta()
    {
        var ids = ModuleCatalog.All.Select(ModuleCatalog.IdOf).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ModuleCatalog.All, m =>
        {
            Assert.True(ModuleCatalog.TryGetModule(ModuleCatalog.IdOf(m), out var found));
            Assert.Equal(m, found);
        });
    }

    [Fact]
    public void Todos_EnElOrdenDelCatalogo_ConPosComoBase()
    {
        Assert.Equal(
            [
                LicensedModule.Pos, LicensedModule.Inventory, LicensedModule.AdvancedReports, LicensedModule.CreditAndCustomers,
                LicensedModule.CashShifts, LicensedModule.Returns, LicensedModule.Suppliers, LicensedModule.Discounts,
                LicensedModule.Categories,
            ],
            ModuleCatalog.All);
        Assert.Equal(LicensedModule.Pos, ModuleCatalog.Base);
        Assert.Equal(Enumerable.Range(1, 9), ModuleCatalog.All.Select(ModuleCatalog.OrderOf));
    }

    [Fact]
    public void IdentificadorDesconocido_SeIgnora() =>
        Assert.False(ModuleCatalog.TryGetModule(Guid.NewGuid(), out _));
}
