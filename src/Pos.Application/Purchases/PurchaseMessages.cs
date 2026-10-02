using Pos.Domain.Common;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases;

/// <summary>Mensajes de compras, en español.</summary>
public static class PurchaseMessages
{
    public const string SupplierRequired = "Elija el proveedor.";
    public const string SupplierInactive = "El proveedor está inactivo; elija otro o actívelo.";
    public const string SupplierNotFound = "El proveedor ya no existe.";
    public const string InvoiceRequired = "Capture el número de factura.";
    public const string InvoiceDateRequired = "Capture la fecha de la factura.";
    public const string InvoiceDateFuture = "La fecha de la factura no puede ser posterior a hoy.";
    public const string LinesRequired = "Agregue al menos un producto.";
    public const string ProductRepeated = "El producto ya está en la compra; modifique su línea.";
    public const string ProductRequired = "Elija el producto.";
    public const string ProductNotFound = "El producto ya no existe.";
    public const string ProductInactive = "El producto está inactivo.";
    public const string ProductNotTracked = "El producto no controla inventario.";
    public const string CostRequired = "Capture el costo unitario.";
    public const string CostFormat = "Capture el costo como un importe, por ejemplo 12.50.";
    public const string CostTooManyDecimals = "El costo admite máximo 2 decimales.";
    public const string CostTooLarge = "El costo máximo es $999,999.99.";
    public const string TaxFormat = "Capture los impuestos como un importe mayor o igual que 0, por ejemplo 26.00.";
    public const string TaxTooManyDecimals = "Los impuestos admiten máximo 2 decimales.";
    public const string TaxTooLarge = "Los impuestos no pueden exceder $999,999.99.";
    public const string LineAmountTooLarge = "El importe de la línea excede $999,999.99.";
    public const string SubtotalTooLarge = "El subtotal excede $999,999.99; registre la factura en dos compras.";
    public const string TotalTooLarge = "El total excede $999,999.99; registre la factura en dos compras.";
    public const string SubtotalZero = "El subtotal debe ser mayor que $0.00: al menos una línea debe tener costo.";
    public const string StockExceeded = "La existencia excedería el máximo permitido.";
    public const string AlreadyVoided = "La compra ya está anulada.";
    public const string ReasonRequired = "El motivo es obligatorio.";
    public const string VoidBlockedTitle = "No se puede anular la compra:";

    public static readonly string InvoiceTooLong = $"El número de factura admite hasta {Purchase.InvoiceNumberMaxLength} caracteres.";
    public static readonly string ReasonTooLong = $"El motivo admite hasta {Purchase.VoidReasonMaxLength} caracteres.";

    public static string ForCost(MoneyParseError error) => error switch
    {
        MoneyParseError.Empty => CostRequired,
        MoneyParseError.TooManyDecimals => CostTooManyDecimals,
        MoneyParseError.TooLarge => CostTooLarge,
        _ => CostFormat,
    };

    public static string ForTax(MoneyParseError error) => error switch
    {
        MoneyParseError.TooManyDecimals => TaxTooManyDecimals,
        MoneyParseError.TooLarge => TaxTooLarge,
        _ => TaxFormat,
    };

    /// <summary>"{producto}: existencia {actual}, se requieren {cantidad}" y las demás causas (FR-017b).</summary>
    public static string ForBlocker(string productName, Abstractions.VoidBlockReason reason, string onHand, string required) => reason switch
    {
        Abstractions.VoidBlockReason.InsufficientStock => $"{productName}: existencia {onHand}, se requieren {required}",
        Abstractions.VoidBlockReason.ProductInactive => $"{productName}: está inactivo; actívelo primero",
        _ => $"{productName}: ya no controla inventario",
    };
}
