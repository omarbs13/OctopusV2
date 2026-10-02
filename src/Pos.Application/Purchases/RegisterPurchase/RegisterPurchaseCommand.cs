namespace Pos.Application.Purchases.RegisterPurchase;

/// <summary>Compra capturada: proveedor, factura, fecha, líneas e impuestos (vacío = $0.00, FR-010a).</summary>
public sealed record RegisterPurchaseCommand(
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    IReadOnlyList<PurchaseLineInput> Lines,
    string? TaxText);
