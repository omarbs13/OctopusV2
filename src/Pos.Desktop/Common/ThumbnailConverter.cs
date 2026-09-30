using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace Pos.Desktop.Common;

/// <summary>
/// Convierte los bytes de una imagen (WEBP) en un <see cref="Bitmap"/>. Sin imagen, o si los bytes
/// no se pueden leer, devuelve nulo y la vista muestra el indicador neutro (003, FR-026).
/// </summary>
public sealed class ThumbnailConverter : IValueConverter
{
    public static ThumbnailConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
#pragma warning disable CA1031 // Una imagen ilegible nunca debe romper el listado; se registra y se muestra el indicador.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.ForContext("Operation", "MostrarMiniatura").Warning(ex, "No se pudo leer una imagen de producto ({Bytes} bytes)", bytes.Length);
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
