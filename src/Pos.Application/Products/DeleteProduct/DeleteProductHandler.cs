using Pos.Application.Abstractions;

namespace Pos.Application.Products.DeleteProduct;

/// <summary>Borrado lógico (FR-021): el producto deja de mostrarse, pero su registro se conserva.</summary>
public sealed class DeleteProductHandler
{
    private readonly IProductRepository _products;
    private readonly IClock _clock;

    public DeleteProductHandler(IProductRepository products, IClock clock)
    {
        _products = products;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await _products.GetAsync(command.Id, cancellationToken);
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
