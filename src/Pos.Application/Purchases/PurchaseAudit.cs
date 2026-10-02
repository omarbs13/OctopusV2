using Pos.Application.Audit;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases;

/// <summary>Datos de la bitácora de compras (research §14): quién, cuánto y cuándo, sin las líneas.</summary>
internal static class PurchaseAudit
{
    public const string State = "Estado";

    /// <summary>"Compra {factura} · {proveedor}".</summary>
    public static string EntityName(Purchase purchase) => $"Compra {purchase.InvoiceNumber} · {purchase.SupplierName}";

    public static IReadOnlyList<AuditField> Snapshot(Purchase purchase) =>
    [
        new("Proveedor", purchase.SupplierName),
        new("Factura", purchase.InvoiceNumber),
        new("Fecha de factura", purchase.InvoiceDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)),
        new("Líneas", purchase.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        .. Amounts(purchase),
    ];

    public static IReadOnlyList<AuditField> Amounts(Purchase purchase) =>
    [
        new("Subtotal", AuditFormat.Money(purchase.SubtotalCents)),
        new("Impuestos", AuditFormat.Money(purchase.TaxCents)),
        new("Total", AuditFormat.Money(purchase.TotalCents)),
    ];
}
