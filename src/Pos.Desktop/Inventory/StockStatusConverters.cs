using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Inventory;

/// <summary>Color de fondo de la etiqueta de estado: neutro, advertencia o error. El texto la distingue además del color.</summary>
public sealed class StockStatusBrushConverter : IValueConverter
{
    private static readonly IBrush Normal = new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));
    private static readonly IBrush Low = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0xA6, 0x23));
    private static readonly IBrush Out = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x3C, 0x31));

    public static StockStatusBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StockStatus.Out => Out,
        StockStatus.Low => Low,
        _ => Normal,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
