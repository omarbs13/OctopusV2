namespace Pos.Application.Inventory.SearchStock;

/// <summary>Búsqueda paginada de existencias.</summary>
/// <param name="Text">Texto buscado; nulo o vacío lista todo.</param>
/// <param name="Filter">Estado de existencia.</param>
/// <param name="IncludeInactive">Incluir productos inactivos.</param>
/// <param name="Page">Página solicitada, base 1.</param>
public sealed record SearchStockQuery(string? Text, StockFilter Filter, bool IncludeInactive, int Page = 1);
