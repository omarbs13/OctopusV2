using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Reports.SetProductCritical;

public sealed record SetProductCriticalCommand(Guid ProductId, bool IsCritical);

/// <summary>Marca o desmarca un producto como crítico; solo quien tiene <c>ManageProducts</c> (research §10).</summary>
public sealed class SetProductCriticalHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;

    public SetProductCriticalHandler(IAccessControl access, IProductRepository products)
    {
        _access = access;
        _products = products;
    }

    public async Task<Result> HandleAsync(SetProductCriticalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        var product = await _products.GetAsync(command.ProductId, includeImage: false, cancellationToken);
        if (product is null)
        {
            return Result.Failure(new NotFound());
        }

        if (product.IsCritical == command.IsCritical)
        {
            return Result.Success();
        }

        product.MarkCritical(command.IsCritical);
        var outcome = await _products.SaveChangesAsync(product, product.Version, cancellationToken);
        return outcome.Status == SaveStatus.Saved ? Result.Success() : Result.Failure(new Conflict());
    }
}
