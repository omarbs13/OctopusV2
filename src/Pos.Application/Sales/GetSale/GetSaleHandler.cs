using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.GetSale;

/// <summary>Detalle de una venta con los valores que se guardaron al venderla; un cajero solo ve las suyas.</summary>
public sealed class GetSaleHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;

    public GetSaleHandler(IAccessControl access, ICurrentUser currentUser, ISaleRepository sales)
    {
        _access = access;
        _currentUser = currentUser;
        _sales = sales;
    }

    public async Task<Result<SaleDetailDto>> HandleAsync(GetSaleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var own = await _access.CheckAsync(Permission.ViewOwnSales, cancellationToken);
        if (!own.Allowed)
        {
            return Result.Failure<SaleDetailDto>(own.Error!);
        }

        var detail = await _sales.GetDetailAsync(query.SaleId, cancellationToken);
        if (detail is null)
        {
            return Result.Failure<SaleDetailDto>(new NotFound());
        }

        if (await SaleAccess.CheckOwnershipAsync(_access, _currentUser, detail.CreatedById, cancellationToken) is { } forbidden)
        {
            return Result.Failure<SaleDetailDto>(forbidden);
        }

        return Result.Success(detail);
    }
}
