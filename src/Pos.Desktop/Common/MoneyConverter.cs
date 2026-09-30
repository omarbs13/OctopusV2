using System.Globalization;
using Avalonia.Data.Converters;

namespace Pos.Desktop.Common;

/// <summary>Muestra centavos como pesos mexicanos: 123450 → "$1,234.50".</summary>
public sealed class MoneyConverter : IValueConverter
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static MoneyConverter Instance { get; } = new();

    public static string Format(long cents) => (cents / 100m).ToString("C2", Culture);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long cents ? Format(cents) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
