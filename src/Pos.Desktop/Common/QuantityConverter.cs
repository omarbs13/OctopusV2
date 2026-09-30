using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Pos.Desktop.Common;

/// <summary>
/// Muestra milésimas con los decimales de su unidad, con separador de miles en es-MX: "12", "1,250"
/// o "1.250" (kg). Solo formatea; el cálculo vive en <c>Quantity</c>. Como convertidor múltiple
/// recibe (milésimas, decimales) y muestra "—" si no hay cantidad.
/// </summary>
public sealed class QuantityConverter : IMultiValueConverter
{
    public const string NoValue = "—";

    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static QuantityConverter Instance { get; } = new();

    public static string Format(long thousandths, int decimalPlaces) =>
        (thousandths / 1000m).ToString("N" + Math.Clamp(decimalPlaces, 0, 3), Culture);

    public static string FormatOrDash(long? thousandths, int decimalPlaces) =>
        thousandths is { } value ? Format(value, decimalPlaces) : NoValue;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 2 || values[0] is not long thousandths)
        {
            return values.Count > 0 && values[0] is null ? NoValue : BindingOperations.DoNothing;
        }

        return Format(thousandths, values[1] is int places ? places : 0);
    }
}
