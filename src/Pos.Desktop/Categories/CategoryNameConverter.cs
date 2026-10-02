using System.Globalization;
using Avalonia.Data.Converters;
using Pos.Application.Categories;

namespace Pos.Desktop.Categories;

/// <summary>Nombre de categoría para listados: "Sin categoría" si es nulo y "(inactiva)" si corresponde.</summary>
public sealed class CategoryNameConverter : IMultiValueConverter
{
    public static CategoryNameConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        var name = values.Count > 0 ? values[0] as string : null;
        var isActive = values.Count < 2 || values[1] is not false;
        return CategoryMessages.Display(name, isActive);
    }
}
