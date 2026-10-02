namespace Pos.Application.Purchases.CalculatePurchaseTotals;

/// <summary>Línea de la captura con los decimales y el nombre de la unidad del producto.</summary>
public sealed record PurchaseTotalsLine(Guid ProductId, int DecimalPlaces, string UnitName, string? QuantityText, string? UnitCostText);

/// <summary>Totales en vivo de la captura de una compra; impuestos vacíos = $0.00 (FR-010a).</summary>
public sealed record CalculatePurchaseTotalsQuery(IReadOnlyList<PurchaseTotalsLine> Lines, string? TaxText);
