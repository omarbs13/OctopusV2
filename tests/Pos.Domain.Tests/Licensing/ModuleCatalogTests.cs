using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>012, FR-007 y FR-008: identificadores fijos y únicos; uno desconocido se ignora.</summary>
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
    public void IdentificadorDesconocido_SeIgnora() =>
        Assert.False(ModuleCatalog.TryGetModule(Guid.NewGuid(), out _));
}
