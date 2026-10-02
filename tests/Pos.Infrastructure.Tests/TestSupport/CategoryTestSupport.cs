using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Categories.CreateCategory;
using Pos.Application.Categories.DeleteCategory;
using Pos.Application.Categories.SearchCategories;
using Pos.Application.Categories.SetCategoryActive;
using Pos.Application.Categories.UpdateCategory;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.SearchProducts;
using Pos.Application.Products.UpdateProduct;
using Pos.Domain.Categories;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Arma los casos de uso de categorías (016) sobre SQLite real con usuarios y permisos reales; un ámbito
/// (contexto) por operación como la composición.
/// </summary>
public sealed class CategoryTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;
    private int _productSequence;

    public CategoryTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
    }

    public ShiftTestSupport Users { get; }

    public async Task<Result<Guid>> CreateAsync(string name, string? description = null)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateCategoryHandler(
                Users.Access(context),
                new CategoryRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new CreateCategoryValidator(),
                NullLogger<CreateCategoryHandler>.Instance)
            .HandleAsync(new CreateCategoryCommand(name, description), Ct);
    }

    /// <summary>Categoría que debe crearse.</summary>
    public async Task<Guid> CreateOkAsync(string name)
    {
        var result = await CreateAsync(name);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Result> UpdateAsync(Guid id, string name, string? description = null, int? expectedVersion = null)
    {
        var version = expectedVersion ?? (await LoadAsync(id)).Version;
        await using var context = _db.CreateDbContext();
        return await new UpdateCategoryHandler(
                Users.Access(context),
                new CategoryRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                new UpdateCategoryValidator(),
                NullLogger<UpdateCategoryHandler>.Instance)
            .HandleAsync(new UpdateCategoryCommand(id, name, description, version), Ct);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool active, bool confirmed = false)
    {
        var version = (await LoadAsync(id)).Version;
        await using var context = _db.CreateDbContext();
        return await new SetCategoryActiveHandler(
                Users.Access(context),
                new CategoryRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                NullLogger<SetCategoryActiveHandler>.Instance)
            .HandleAsync(new SetCategoryActiveCommand(id, active, version, confirmed), Ct);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var version = (await LoadAsync(id)).Version;
        await using var context = _db.CreateDbContext();
        return await new DeleteCategoryHandler(
                Users.Access(context),
                new CategoryRepository(context),
                new AuditLog(context),
                new WriteTransactions(context),
                _db.Clock,
                NullLogger<DeleteCategoryHandler>.Instance)
            .HandleAsync(new DeleteCategoryCommand(id, version), Ct);
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> SearchAsync(string? text = null, CategoryStatusFilter status = CategoryStatusFilter.All)
    {
        await using var context = _db.CreateDbContext();
        var result = await new SearchCategoriesHandler(Users.Access(context), new CategoryRepository(context))
            .HandleAsync(new SearchCategoriesQuery(text, status), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Categoría tal como está guardada, aunque esté borrada.</summary>
    public async Task<Category> LoadAsync(Guid id)
    {
        await using var context = _db.CreateDbContext();
        return await context.Categories.AsNoTracking().SingleAsync(c => c.Id == id, Ct);
    }

    /// <summary>Producto insertado directamente, con la categoría indicada.</summary>
    public async Task<Product> AddProductAsync(Guid? categoryId, string? name = null, bool active = true, bool deleted = false)
    {
        var sequence = Interlocked.Increment(ref _productSequence);
        var product = Product.Create(name ?? $"Producto {sequence}", $"CAT-{sequence}", null, Money.FromCents(1_000), "H87", categoryId: categoryId);
        if (!active)
        {
            product.Update(product.Name, product.Sku, null, product.Price, product.UnitCode, isActive: false, categoryId: categoryId);
        }

        if (deleted)
        {
            product.Delete(_db.Clock.UtcNow);
        }

        await using var context = _db.CreateDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync(Ct);
        return product;
    }

    public async Task<Product> LoadProductAsync(Guid id)
    {
        await using var context = _db.CreateDbContext();
        return await context.Products.AsNoTracking().SingleAsync(p => p.Id == id, Ct);
    }

    public async Task<int> AuditCountAsync(string action, Guid categoryId)
    {
        await using var context = _db.CreateDbContext();
        return await context.AuditEntries.CountAsync(e => e.Action == action && e.EntityId == categoryId, Ct);
    }

    /// <summary>Alta de producto con el caso de uso real (016, FR-011).</summary>
    public async Task<Result<ProductDto>> CreateProductAsync(string sku, Guid? categoryId)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateProductHandler(
                Users.Access(context),
                new ProductRepository(context),
                new CreateProductValidator(),
                new CategoryRepository(context),
                new WriteTransactions(context),
                new AuditLog(context),
                NullLogger<CreateProductHandler>.Instance)
            .HandleAsync(new CreateProductCommand($"Producto {sku}", sku, null, "10.00", "H87", CategoryId: categoryId), Ct);
    }

    /// <summary>Edición con el caso de uso real: cambia el nombre y asigna <paramref name="categoryId"/>.</summary>
    public async Task<Result<ProductDto>> UpdateProductAsync(Guid productId, string name, Guid? categoryId)
    {
        var current = await LoadProductAsync(productId);
        await using var context = _db.CreateDbContext();
        return await new UpdateProductHandler(
                Users.Access(context),
                new ProductRepository(context),
                new UpdateProductValidator(),
                new InventoryRepository(context),
                new WriteTransactions(context),
                new CategoryRepository(context),
                new AuditLog(context),
                NullLogger<UpdateProductHandler>.Instance)
            .HandleAsync(
                new UpdateProductCommand(productId, current.Version, name, current.Sku, current.Barcode, "10.00", current.UnitCode, current.IsActive, CategoryId: categoryId),
                Ct);
    }

    public async Task<ProductPage> SearchProductsAsync(string? text, CategoryFilter category)
    {
        await using var context = _db.CreateDbContext();
        var result = await new SearchProductsHandler(Users.Access(context), new ProductRepository(context))
            .HandleAsync(new SearchProductsQuery(text, IncludeInactive: true, Category: category), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Categoría insertada directamente (para preparar reportes sin pasar por los casos de uso).</summary>
    public static async Task<Guid> AddCategoryAsync(TestDb db, string name, bool active = true)
    {
        var category = Category.Create(name, null);
        if (!active)
        {
            category.Deactivate();
        }

        await using var context = db.CreateDbContext();
        context.Categories.Add(category);
        await context.SaveChangesAsync(Ct);
        return category.Id;
    }

    /// <summary>Cambia la categoría vigente del producto conservando el resto de sus datos.</summary>
    public static async Task AssignAsync(TestDb db, Guid productId, Guid? categoryId)
    {
        await using var context = db.CreateDbContext();
        var product = await context.Products.SingleAsync(p => p.Id == productId, Ct);
        product.Update(
            product.Name,
            product.Sku,
            product.Barcode,
            product.Price,
            product.UnitCode,
            product.IsActive,
            product.TracksInventory,
            product.MinimumStock,
            hasMovements: false,
            categoryId: categoryId);
        await context.SaveChangesAsync(Ct);
    }
}
