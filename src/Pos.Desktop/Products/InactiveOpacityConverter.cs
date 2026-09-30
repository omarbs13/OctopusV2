using System.Globalization;
using Avalonia.Data.Converters;

namespace Pos.Desktop.Products;

/// <summary>Atenúa las filas de productos inactivos (003, FR-002); la etiqueta de estado las distingue además del tono.</summary>
public sealed class InactiveOpacityConverter : IValueConverter
{
    public const double InactiveOpacity = 0.55;

    public static InactiveOpacityConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? InactiveOpacity : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
