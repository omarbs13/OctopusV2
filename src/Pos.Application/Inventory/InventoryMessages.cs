using Pos.Domain.Common;

namespace Pos.Application.Inventory;

/// <summary>Mensajes de inventario, en español.</summary>
public static class InventoryMessages
{
    public const string NotTracked = "Este producto no controla inventario.";
    public const string ProductInactive = "El producto está inactivo; actívelo para registrar movimientos.";
    public const string InitialNotAllowed = "Este producto ya tiene movimientos; registre un ajuste en lugar de inventario inicial.";
    public const string QuantityRequired = "Capture la cantidad.";
    public const string QuantityNotPositive = "La cantidad debe ser mayor que 0.";
    public const string QuantityFormat = "Capture la cantidad como un número, por ejemplo 12 o 1.250.";
    public const string QuantityTooLarge = "La cantidad máxima es 9,999,999.999.";
    public const string StockExceeded = "La existencia excedería el máximo permitido.";
    public const string ReasonRequired = "El motivo es obligatorio en los ajustes.";
    public const string ReasonTooLong = "El motivo admite hasta 250 caracteres.";
    public const string ReferenceTooLong = "La referencia admite hasta 50 caracteres.";
    public const string TypeInvalid = "El tipo de movimiento no es válido.";
    public const string DateRangeInvalid = "La fecha inicial no puede ser posterior a la final.";
    public const string StockChanged = "La existencia cambió mientras capturaba; revise y vuelva a intentar.";

    public const string MinimumTooLarge = "La existencia mínima no puede exceder 9,999,999.999.";
    public const string MinimumFormat = "Capture la existencia mínima como un número, por ejemplo 5 o 1.250.";
    public const string ReorderPointTooLarge = "El punto de reorden no puede exceder 9,999,999.999.";
    public const string ReorderPointFormat = "Capture el punto de reorden como un número, por ejemplo 5 o 1.250.";
    public const string UnitLocked = "No se puede cambiar la unidad de un producto con movimientos de inventario.";
    public const string TrackingLocked = "No se puede dejar de controlar el inventario de un producto con movimientos.";

    public static string NoDecimals(string unitName) => $"{unitName} no admite decimales.";

    public static string MaxDecimals(string subject) => $"{subject} admite máximo 3 decimales.";

    public static string WouldGoNegative(string onHand, string unitName) =>
        $"La existencia actual es {onHand} {unitName}; el ajuste la dejaría por debajo de cero.";

    /// <summary>Mensaje de un error de captura de la cantidad de un movimiento.</summary>
    public static string ForQuantity(QuantityParseError error, string unitName, int decimalPlaces) => error switch
    {
        QuantityParseError.Empty => QuantityRequired,
        QuantityParseError.NotPositive => QuantityNotPositive,
        QuantityParseError.Format => QuantityFormat,
        QuantityParseError.TooManyDecimals => decimalPlaces == 0 ? NoDecimals(unitName) : MaxDecimals("La cantidad"),
        _ => QuantityTooLarge,
    };

    /// <summary>Mensaje de un error de captura de la existencia mínima (sujeto "La existencia mínima").</summary>
    public static string ForMinimum(QuantityParseError error, string unitName, int decimalPlaces) => error switch
    {
        QuantityParseError.TooManyDecimals => decimalPlaces == 0 ? NoDecimals(unitName) : MaxDecimals("La existencia mínima"),
        QuantityParseError.TooLarge => MinimumTooLarge,
        _ => MinimumFormat,
    };

    /// <summary>Mensaje de un error de captura del punto de reorden (sujeto "El punto de reorden", 022).</summary>
    public static string ForReorderPoint(QuantityParseError error, string unitName, int decimalPlaces) => error switch
    {
        QuantityParseError.TooManyDecimals => decimalPlaces == 0 ? NoDecimals(unitName) : MaxDecimals("El punto de reorden"),
        QuantityParseError.TooLarge => ReorderPointTooLarge,
        _ => ReorderPointFormat,
    };

    /// <summary>Formatea una cantidad en milésimas con los decimales de su unidad, para mensajes.</summary>
    public static string Format(long thousandths, int decimalPlaces)
    {
        var text = Quantity.FromThousandths(Math.Abs(thousandths)).ToEditableString(decimalPlaces);
        return thousandths < 0 ? "-" + text : text;
    }
}
