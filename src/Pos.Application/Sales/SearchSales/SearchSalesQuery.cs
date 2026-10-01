using Pos.Domain.Sales;

namespace Pos.Application.Sales.SearchSales;

/// <summary>
/// Filtros de "Ventas realizadas"; el límite superior de fecha es exclusivo (UTC). <c>CashierId</c> solo
/// lo puede pedir quien ve todas las ventas (007, FR-026). <c>CustomerId</c> filtra las ventas a crédito
/// de un cliente (014).
/// </summary>
public sealed record SearchSalesQuery(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    string? FolioText,
    SaleStatus? Status,
    int Page = 1,
    Guid? CashierId = null,
    Guid? CustomerId = null);
