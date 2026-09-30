using Pos.Domain.Sales;

namespace Pos.Application.Sales.SearchSales;

/// <summary>Filtros de "Ventas realizadas"; el límite superior de fecha es exclusivo (UTC).</summary>
public sealed record SearchSalesQuery(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    string? FolioText,
    SaleStatus? Status,
    int Page = 1);
