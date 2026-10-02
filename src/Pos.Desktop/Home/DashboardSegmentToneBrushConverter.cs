using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pos.Desktop.Home;

/// <summary>Fondo de una cifra de tarjeta, con los tonos de las etiquetas de estado; la etiqueta la distingue además del color.</summary>
public sealed class DashboardSegmentToneBrushConverter : IValueConverter
{
    private static readonly IBrush Neutral = new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));
    private static readonly IBrush Warning = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0xA6, 0x23));
    private static readonly IBrush Danger = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x3C, 0x31));

    public static DashboardSegmentToneBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DashboardSegmentTone.Danger => Danger,
        DashboardSegmentTone.Warning => Warning,
        _ => Neutral,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
