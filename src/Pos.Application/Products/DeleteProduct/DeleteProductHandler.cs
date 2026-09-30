using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.DeleteProduct;

/// <summary>Borrado lógico (FR-021): el producto deja de mostrarse, pero su registro se conserva.</summary>
public sealed class DeleteProductHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IClock _clock;

    public DeleteProductHandler(IAccessControl access, IProductRepository products, IClock clock)
    {
        _access = access;
        _products = products;
        _clock = clock;
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

        product.Delete(_clock.UtcNow);
        var outcome = await _products.SaveChangesAsync(product, command.ExpectedVersion, cancellationToken);
        return outcome.Status == SaveStatus.Saved ? Result.Success() : Result.Failure(new Conflict());
    }
}
