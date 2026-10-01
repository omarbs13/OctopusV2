using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Sales.SearchSales;

/// <summary>Ventas realizadas, de la más reciente a la más antigua, paginadas de 100 en 100.</summary>
public sealed class SearchSalesHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;

    public SearchSalesHandler(IAccessControl access, ICurrentUser currentUser, ISaleRepository sales)
    {
        _access = access;
        _currentUser = currentUser;
        _sales = sales;
    }

    public async Task<Result<SalePage>> HandleAsync(SearchSalesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var own = await _access.CheckAsync(Permission.ViewOwnSales, cancellationToken);
        if (!own.Allowed)
        {
            return Result.Failure<SalePage>(own.Error!);
        }

        // Sin ver todas las ventas, el filtro por cajero se fuerza al usuario actual (FR-026).
        var viewAll = await _access.CheckAsync(Permission.ViewAllSales, cancellationToken);
        if (!viewAll.Allowed && query.CashierId is { } requested && requested != _currentUser.UserId)
        {
            return Result.Failure<SalePage>(viewAll.Error!);
        }

        var cashierId = viewAll.Allowed ? query.CashierId : _currentUser.UserId;

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
            SalePage.DefaultPageSize,
            cashierId,
            query.CustomerId);
        return Result.Success(await _sales.SearchAsync(search, cancellationToken));
    }
}
