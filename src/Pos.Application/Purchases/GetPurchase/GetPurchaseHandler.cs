using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Purchases.GetPurchase;

/// <summary>Detalle de una compra (FR-022); lo ve quien consulta el reporte o quien registra compras.</summary>
public sealed class GetPurchaseHandler
{
    private readonly IAccessControl _access;
    private readonly IPurchaseRepository _purchases;

    public GetPurchaseHandler(IAccessControl access, IPurchaseRepository purchases)
    {
        _access = access;
        _purchases = purchases;
    }

    public async Task<Result<PurchaseDetailDto>> HandleAsync(GetPurchaseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _access.HasAsync(Permission.RegisterPurchases, cancellationToken))
        {
            var access = await _access.CheckAsync(Permission.ViewPurchaseReport, cancellationToken);
            if (!access.Allowed)
            {
                return Result.Failure<PurchaseDetailDto>(access.Error!);
            }
        }

        var detail = await _purchases.GetDetailAsync(query.PurchaseId, cancellationToken);
        return detail is null ? Result.Failure<PurchaseDetailDto>(new NotFound()) : Result.Success(detail);
    }
}
