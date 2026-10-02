namespace Pos.Application.Sales.FindProductsForSale;

/// <summary>
/// Texto capturado en el Punto de venta: código de barras, SKU o parte del nombre. Con
/// <paramref name="FromScanner"/> (lectura del escáner, 021) no se busca por nombre.
/// </summary>
public sealed record FindProductsForSaleQuery(string Text, bool FromScanner = false);
