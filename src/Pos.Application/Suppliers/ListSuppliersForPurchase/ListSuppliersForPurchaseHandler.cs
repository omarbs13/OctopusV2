using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.ListSuppliersForPurchase;

/// <summary>Selector de proveedor de la compra: solo activos, hasta 50, por nombre o RFC (FR-007).</summary>
public sealed class ListSuppliersForPurchaseHandler
{
    public const int Limit = 50;

    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;

    public ListSuppliersForPurchaseHandler(IAccessControl access, ISupplierRepository suppliers)
    {
        _access = access;
        _suppliers = suppliers;
    }

    public async Task<Result<IReadOnlyList<SupplierOption>>> HandleAsync(ListSuppliersForPurchaseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.RegisterPurchases, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<SupplierOption>>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        return Result.Success(await _suppliers.ListActiveAsync(text, Limit, cancellationToken));
    }
}
