using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Categories;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.DeleteProduct;

/// <summary>
/// Borrado lógico (FR-021): el producto deja de mostrarse, pero su registro se conserva. La bitácora
/// guarda sus últimos valores en el mismo guardado (018, FR-004).
/// </summary>
public sealed class DeleteProductHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IClock _clock;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLog _audit;

    public DeleteProductHandler(IAccessControl access, IProductRepository products, IClock clock, ICategoryRepository categories, IAuditLog audit)
    {
        _access = access;
        _products = products;
        _clock = clock;
        _categories = categories;
        _audit = audit;
    }

    public async Task<Result> HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        var product = await _products.GetAsync(command.Id, includeImage: false, cancellationToken);
        if (product is null)
        {
            return Result.Failure(new NotFound());
        }

        if (product.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        var category = product.CategoryId is { } categoryId ? await _categories.GetAsync(categoryId, cancellationToken) : null;
        var hasImage = await _products.HasImageAsync(product.Id, cancellationToken);
        _audit.Add(new AuditRecord(
            AuditActions.ProductDeleted,
            AuditActions.ProductEntity,
            product.Id,
            EntityName: product.Name,
            Changes: AuditChanges.Removed(ProductAuditFields.Snapshot(product, category?.Name, hasImage))));

        product.Delete(_clock.UtcNow);
        var outcome = await _products.SaveChangesAsync(product, command.ExpectedVersion, cancellationToken);
        return outcome.Status == SaveStatus.Saved ? Result.Success() : Result.Failure(new Conflict());
    }
}
