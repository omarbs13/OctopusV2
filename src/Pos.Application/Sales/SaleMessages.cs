namespace Pos.Application.Sales;

/// <summary>Mensajes de ventas, en español.</summary>
public static class SaleMessages
{
    public const string TextRequired = "Capture un código o un nombre.";
    public const string LinesRequired = "La venta debe tener al menos un producto.";
    public const string ProductRepeated = "Un producto no puede repetirse en la venta.";
    public const string QuantityInvalid = "La cantidad de una línea no es válida.";
    public const string PriceInvalid = "El precio de una línea no es válido.";
    public const string PaymentInvalid = "Un pago no es válido.";
    public const string PaymentsRequired = "Capture el pago de la venta.";
    public const string PaymentShort = "El pago no cubre el total de la venta.";
    public const string ReferenceTooLong = "La referencia admite hasta 50 caracteres.";
    public const string ReasonRequired = "Capture el motivo de la cancelación.";
    public const string ReasonTooLong = "El motivo admite hasta 250 caracteres.";
    public const string FolioInvalid = "Capture un folio válido, por ejemplo V-000123 o 123.";
    public const string DateRangeInvalid = "La fecha inicial no puede ser posterior a la final.";
    public const string AlreadyCancelled = "Esta venta ya está cancelada";
    public const string SaleChanged = "Los precios o la disponibilidad cambiaron; revise el total antes de cobrar";
    public const string TooManyLines = "La venta admite hasta 500 líneas.";
    public const string OnAccountExclusive = "La venta a crédito debe tener un único pago por el total.";
    public const string CustomerRequired = "Elija el cliente de la venta a crédito.";

    public static string Shortfall(string amount) => $"Faltan {amount} para cubrir la venta.";
}
