using Pos.Application.Abstractions;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.SearchSales;

/// <summary>Ventas realizadas, de la más reciente a la más antigua, paginadas de 100 en 100.</summary>
public sealed class SearchSalesHandler
{
    private readonly ISaleRepository _sales;

    public SearchSalesHandler(ISaleRepository sales) => _sales = sales;

    public async Task<Result<SalePage>> HandleAsync(SearchSalesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var errors = new List<FieldError>();
        long? folioNumber = null;
        if (!string.IsNullOrWhiteSpace(query.FolioText))
        {
            if (Folio.TryParse(query.FolioText, out var number))
            {
                folioNumber = number;
            }
            else
            {
                errors.Add(new FieldError(SaleFields.Folio, SaleMessages.FolioInvalid));
            }
        }

        if (query.FromUtc is { } from && query.ToUtcExclusive is { } to && from > to)
        {
            errors.Add(new FieldError(SaleFields.DateRange, SaleMessages.DateRangeInvalid));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<SalePage>(new ValidationFailed(errors));
        }

        var search = new SaleSearch(
            query.FromUtc,
            query.ToUtcExclusive,
            folioNumber,
            query.Status,
            Math.Max(query.Page, 1),
            SalePage.DefaultPageSize);
        return Result.Success(await _sales.SearchAsync(search, cancellationToken));
    }
}
