using Pos.Application.Abstractions;

namespace Pos.Application.Products;

/// <summary>
/// Valida y optimiza una imagen de producto (003, FR-022 a FR-024). La implementación vive en
/// Infrastructure; es síncrona y sin E/S más allá del flujo recibido.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Identifica el formato por su contenido, aplica la orientación EXIF, reduce la imagen a
    /// <see cref="Domain.Products.Product.ImageMaxSide"/> px y genera la miniatura. Nunca lanza por
    /// un archivo inválido: devuelve el motivo.
    /// </summary>
    ImageProcessingResult Process(Stream content);
}

/// <summary>Resultado del procesamiento: la imagen optimizada y su miniatura, o el motivo del rechazo.</summary>
public sealed record ImageProcessingResult(
    byte[]? Content,
    byte[]? Thumbnail,
    int Width,
    int Height,
    InvalidImageReason? Error)
{
    public static ImageProcessingResult Success(byte[] content, byte[] thumbnail, int width, int height) =>
        new(content, thumbnail, width, height, null);

    public static ImageProcessingResult Failure(InvalidImageReason reason) => new(null, null, 0, 0, reason);
}
