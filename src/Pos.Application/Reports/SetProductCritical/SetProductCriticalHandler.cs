using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Reports.SetProductCritical;

public sealed record SetProductCriticalCommand(Guid ProductId, bool IsCritical);

/// <summary>
/// Marca o desmarca un producto como crítico; solo quien tiene <c>ManageProducts</c> (research §10). El
/// cambio queda en la bitácora como una modificación del producto (018, research §5).
/// </summary>
public sealed class SetProductCriticalHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IAuditLog _audit;

    public SetProductCriticalHandler(IAccessControl access, IProductRepository products, IAuditLog audit)
    {
        _access = access;
        _products = products;
        _audit = audit;
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

        _audit.Add(new AuditRecord(
            AuditActions.ProductUpdated,
            AuditActions.ProductEntity,
            product.Id,
            EntityName: product.Name,
            Changes: [new AuditFieldChange(ProductAuditFields.Critical, AuditFormat.YesNo(product.IsCritical), AuditFormat.YesNo(command.IsCritical))]));
        product.MarkCritical(command.IsCritical);
        var outcome = await _products.SaveChangesAsync(product, product.Version, cancellationToken);
        return outcome.Status == SaveStatus.Saved ? Result.Success() : Result.Failure(new Conflict());
    }
}
