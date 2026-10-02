using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Categories;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Categories;

/// <summary>016, Historia 1: integridad del catálogo de categorías sobre SQLite real con permisos reales.</summary>
public sealed class CategoryUseCaseTests : IAsyncLifetime
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
    public async Task NombreRepetido_SinMayusculasNiAcentos_SeRechazaAunqueEsteInactiva_YSeLiberaAlEliminar()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        Assert.Equal(1, await _categories.AuditCountAsync(AuditActions.CategoryCreated, bebidas));

        Assert.Equal(new Duplicate(CategoryFields.Name), (await _categories.CreateAsync("bebidas")).Error);
        Assert.True((await _categories.SetActiveAsync(bebidas, active: false)).IsSuccess);
        Assert.Equal(new Duplicate(CategoryFields.Name), (await _categories.CreateAsync("  BEBÍDAS ")).Error);

        Assert.True((await _categories.DeleteAsync(bebidas)).IsSuccess);
        Assert.True((await _categories.CreateAsync("Bebidas")).IsSuccess);
    }

    [Fact]
    public async Task Desactivar_ConProductos_PideConfirmacion_YConfirmadaConservaLaCategoriaDelProducto()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        var product = await _categories.AddProductAsync(bebidas);

        var unconfirmed = await _categories.SetActiveAsync(bebidas, active: false);

        Assert.Equal(new ConfirmationRequired(1), unconfirmed.Error);
        Assert.True((await _categories.LoadAsync(bebidas)).IsActive);
        Assert.Equal(0, await _categories.AuditCountAsync(AuditActions.CategoryDeactivated, bebidas));

        Assert.True((await _categories.SetActiveAsync(bebidas, active: false, confirmed: true)).IsSuccess);
        Assert.False((await _categories.LoadAsync(bebidas)).IsActive);
        Assert.Equal(bebidas, (await _categories.LoadProductAsync(product.Id)).CategoryId);
        Assert.Equal(1, await _categories.AuditCountAsync(AuditActions.CategoryDeactivated, bebidas));

        Assert.True((await _categories.SetActiveAsync(bebidas, active: true)).IsSuccess);
        Assert.Equal(1, await _categories.AuditCountAsync(AuditActions.CategoryActivated, bebidas));
    }

    [Fact]
    public async Task Desactivar_SinProductos_NoPideConfirmacion()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        await _categories.AddProductAsync(bebidas, deleted: true);

        Assert.True((await _categories.SetActiveAsync(bebidas, active: false)).IsSuccess);
    }

    [Fact]
    public async Task Eliminar_ConUnProductoInactivo_SeRechazaConElConteo()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        await _categories.AddProductAsync(bebidas, active: false);

        var result = await _categories.DeleteAsync(bebidas);

        Assert.Equal(new CategoryInUse(1), result.Error);
        Assert.Null((await _categories.LoadAsync(bebidas)).DeletedAt);
        Assert.Equal(0, await _categories.AuditCountAsync(AuditActions.CategoryDeleted, bebidas));
    }

    [Fact]
    public async Task Eliminar_ConSoloProductosBorrados_SeEliminaYLosProductosQuedanSinCategoria()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        var deleted = await _categories.AddProductAsync(bebidas, deleted: true);

        Assert.True((await _categories.DeleteAsync(bebidas)).IsSuccess);

        Assert.NotNull((await _categories.LoadAsync(bebidas)).DeletedAt);
        Assert.Null((await _categories.LoadProductAsync(deleted.Id)).CategoryId);
        Assert.Empty(await _categories.SearchAsync());
        Assert.Equal(1, await _categories.AuditCountAsync(AuditActions.CategoryDeleted, bebidas));
    }

    [Fact]
    public async Task Editar_ConVersionVieja_EsConflicto_YConNombreDeOtra_EsDuplicado()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        await _categories.CreateOkAsync("Botanas");
        Assert.True((await _categories.UpdateAsync(bebidas, "Bebidas frías", "Refrescos y aguas")).IsSuccess);
        Assert.Equal(1, await _categories.AuditCountAsync(AuditActions.CategoryUpdated, bebidas));

        Assert.IsType<Conflict>((await _categories.UpdateAsync(bebidas, "Otra", expectedVersion: 1)).Error);
        Assert.Equal(new Duplicate(CategoryFields.Name), (await _categories.UpdateAsync(bebidas, "botanas")).Error);
        Assert.Equal("Bebidas frías", (await _categories.LoadAsync(bebidas)).Name);
    }

    [Fact]
    public async Task Buscar_FiltraPorTextoYEstado_OrdenaPorNombre_YCuentaProductosNoBorrados()
    {
        var bebidas = await _categories.CreateOkAsync("Bebidas");
        var botanas = await _categories.CreateOkAsync("Botanas");
        await _categories.CreateOkAsync("Abarrotes");
        await _categories.AddProductAsync(bebidas);
        await _categories.AddProductAsync(bebidas, active: false);
        await _categories.AddProductAsync(bebidas, deleted: true);
        Assert.True((await _categories.SetActiveAsync(botanas, active: false)).IsSuccess);

        Assert.Equal(["Abarrotes", "Bebidas", "Botanas"], (await _categories.SearchAsync()).Select(c => c.Name));
        Assert.Equal(2, Assert.Single(await _categories.SearchAsync("BEB")).ProductCount);
        Assert.Equal("Botanas", Assert.Single(await _categories.SearchAsync(status: CategoryStatusFilter.Inactive)).Name);
        Assert.Equal(["Abarrotes", "Bebidas"], (await _categories.SearchAsync(status: CategoryStatusFilter.Active)).Select(c => c.Name));
    }
}
