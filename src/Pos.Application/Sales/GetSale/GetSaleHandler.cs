using Pos.Application.Abstractions;

namespace Pos.Application.Sales.GetSale;

/// <summary>Detalle de una venta con los valores que se guardaron al venderla.</summary>
public sealed class GetSaleHandler
{
    private readonly ISaleRepository _sales;

    public GetSaleHandler(ISaleRepository sales) => _sales = sales;

    public async Task<Result<SaleDetailDto>> HandleAsync(GetSaleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var detail = await _sales.GetDetailAsync(query.SaleId, cancellationToken);
        return detail is null ? Result.Failure<SaleDetailDto>(new NotFound()) : Result.Success(detail);
    }
}
