using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Categories;

/// <summary>016, Historia 2: asignar categoría a productos sobre SQLite real (FR-009 a FR-012).</summary>
public sealed class ProductCategoryAssignmentTests : IAsyncLifetime
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
    public async Task Alta_SinCategoriaEsValida_YConCategoriaInactivaOBorradaSeRechaza()
    {
        var withoutCategory = await _categories.CreateProductAsync("SIN-1", categoryId: null);
        Assert.True(withoutCategory.IsSuccess, withoutCategory.Error?.ToString());
        Assert.Null((await _categories.LoadProductAsync(withoutCategory.Value.Id)).CategoryId);

        var lacteos = await _categories.CreateOkAsync("Lácteos");
        Assert.True((await _categories.SetActiveAsync(lacteos, active: false)).IsSuccess);
        var temporal = await _categories.CreateOkAsync("Temporal");
        Assert.True((await _categories.DeleteAsync(temporal)).IsSuccess);

        Assert.IsType<CategoryNotAssignable>((await _categories.CreateProductAsync("LAC-1", lacteos)).Error);
        Assert.IsType<CategoryNotAssignable>((await _categories.CreateProductAsync("TMP-1", temporal)).Error);
        Assert.IsType<CategoryNotAssignable>((await _categories.CreateProductAsync("NEW-1", Guid.CreateVersion7())).Error);
        Assert.Equal(1, (await _categories.SearchProductsAsync(null, CategoryFilter.All)).TotalCount);
    }

    [Fact]
    public async Task Edicion_ConservaUnaCategoriaInactivaSinCambiarla_PeroNoPermiteCambiarAOtraInactiva()
    {
        var lacteos = await _categories.CreateOkAsync("Lácteos");
        var quesos = await _categories.CreateOkAsync("Quesos");
        var product = await _categories.CreateProductAsync("LAC-1", lacteos);
        Assert.True(product.IsSuccess, product.Error?.ToString());
        Assert.True((await _categories.SetActiveAsync(lacteos, active: false, confirmed: true)).IsSuccess);
        Assert.True((await _categories.SetActiveAsync(quesos, active: false)).IsSuccess);

        var kept = await _categories.UpdateProductAsync(product.Value.Id, "Leche entera", lacteos);
        Assert.True(kept.IsSuccess, kept.Error?.ToString());
        Assert.Equal((lacteos, "Lácteos", false), (kept.Value.CategoryId, kept.Value.CategoryName, kept.Value.CategoryIsActive));

        var changed = await _categories.UpdateProductAsync(product.Value.Id, "Leche entera", quesos);
        Assert.IsType<CategoryNotAssignable>(changed.Error);
        Assert.Equal(lacteos, (await _categories.LoadProductAsync(product.Value.Id)).CategoryId);
    }

    [Fact]
    public async Task Listado_FiltraPorCategoriaOSinCategoria_CombinadoConTexto_YElConteoSigueLaAsignacion()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        var cola = (await _categories.CreateProductAsync("COLA-1", bebidas)).Value;
        await _categories.CreateProductAsync("AGUA-1", bebidas);
        var papas = (await _categories.CreateProductAsync("PAPA-1", categoryId: null)).Value;

        var onlyBebidas = await _categories.SearchProductsAsync("cola", CategoryFilter.Only(bebidas));
        var item = Assert.Single(onlyBebidas.Items);
        Assert.Equal((cola.Id, "Bebidas"), (item.Id, item.CategoryName));
        Assert.Equal(papas.Id, Assert.Single((await _categories.SearchProductsAsync(null, CategoryFilter.Uncategorized)).Items).Id);
        Assert.Empty((await _categories.SearchProductsAsync("papa", CategoryFilter.Only(bebidas))).Items);
        Assert.Equal(2, Assert.Single(await _categories.SearchAsync()).ProductCount);

        Assert.True((await _categories.UpdateProductAsync(papas.Id, papas.Name, bebidas)).IsSuccess);
        Assert.Equal(3, Assert.Single(await _categories.SearchAsync()).ProductCount);
        Assert.True((await _categories.UpdateProductAsync(cola.Id, cola.Name, categoryId: null)).IsSuccess);
        Assert.Equal(2, Assert.Single(await _categories.SearchAsync()).ProductCount);
    }
}
