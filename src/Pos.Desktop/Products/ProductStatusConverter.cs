using System.Globalization;
using Avalonia.Data.Converters;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Products;

/// <summary>Estado del producto para mostrar: "Activo" o "Inactivo".</summary>
public sealed class ProductStatusConverter : IValueConverter
{
    public static ProductStatusConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.Products_Active : Strings.Products_Inactive;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
