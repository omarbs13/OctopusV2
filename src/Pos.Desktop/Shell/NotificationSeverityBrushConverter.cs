using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pos.Desktop.Shell;

/// <summary>
/// Fondo de una notificación: advertencia o error, con los mismos tonos que la etiqueta de estado de
/// existencias, sin que el shell dependa de inventario. El título la distingue además del color.
/// </summary>
public sealed class NotificationSeverityBrushConverter : IValueConverter
{
    private static readonly IBrush Warning = new SolidColorBrush(Color.FromArgb(0xEE, 0xF5, 0xA6, 0x23));
    private static readonly IBrush Danger = new SolidColorBrush(Color.FromArgb(0xEE, 0xE0, 0x3C, 0x31));

    public static NotificationSeverityBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        NotificationSeverity.Danger => Danger,
        _ => Warning,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
