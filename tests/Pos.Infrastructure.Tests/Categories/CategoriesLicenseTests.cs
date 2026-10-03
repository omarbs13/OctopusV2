using Pos.Application.Abstractions;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Categories;

/// <summary>
/// 025, H1 escenario 5 (FR-007, FR-033): sin el módulo Categorías el selector queda vacío, crear y editar
/// categorías se rechaza sin cambios y los productos conservan la categoría que ya tenían.
/// </summary>
public sealed class CategoriesLicenseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private CategoryTestSupport _categories = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _categories = new CategoryTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SinCategorias_SeRechazaGestionarlas_ElSelectorQuedaVacio_YLosProductosConservanSuCategoria()
    {
        var lacteos = await _categories.CreateOkAsync("Lácteos");
        var quesos = await _categories.CreateOkAsync("Quesos");
        var product = await _categories.CreateProductAsync("LAC-1", lacteos);
        Assert.True(product.IsSuccess, product.Error?.ToString());

        _categories.Users.License = TestLicenses.Licensed(_db.Clock, LicensedModule.Inventory);

        Assert.Equal(LicensedModule.Categories, Assert.IsType<ModuleNotLicensed>((await _categories.CreateAsync("Frutas")).Error).Module);
        Assert.IsType<ModuleNotLicensed>((await _categories.UpdateAsync(lacteos, "Lácteos y derivados")).Error);
        Assert.Equal("Lácteos", (await _categories.LoadAsync(lacteos)).Name);

        var options = await _categories.ListOptionsAsync();
        Assert.True(options.IsSuccess);
        Assert.Empty(options.Value);

        var edited = await _categories.UpdateProductAsync(product.Value.Id, "Leche entera", quesos);
        Assert.True(edited.IsSuccess, edited.Error?.ToString());
        var stored = await _categories.LoadProductAsync(product.Value.Id);
        Assert.Equal("Leche entera", stored.Name);
        Assert.Equal(lacteos, stored.CategoryId);

        var created = await _categories.CreateProductAsync("QUE-1", quesos);
        Assert.True(created.IsSuccess, created.Error?.ToString());
        Assert.Null((await _categories.LoadProductAsync(created.Value.Id)).CategoryId);

        // Al renovar el módulo, las categorías siguen ahí (FR-033).
        _categories.Users.License = TestLicenses.Licensed(_db.Clock, LicensedModule.Categories);
        Assert.Equal(2, (await _categories.ListOptionsAsync()).Value.Count);
    }
}
