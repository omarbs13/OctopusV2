using System.Globalization;
using Avalonia.Data.Converters;

namespace Pos.Desktop.Home;

/// <summary>Convierte la proporción de una barra (0 a 1) en su tamaño en píxeles: proporción × parámetro.</summary>
public sealed class BarSizeConverter : IValueConverter
{
    public static BarSizeConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double ratio || parameter is not string max
            || !double.TryParse(max, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            return 0.0;
        }

        return Math.Clamp(ratio, 0, 1) * size;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
