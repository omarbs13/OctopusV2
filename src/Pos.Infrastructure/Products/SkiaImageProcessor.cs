using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Products;
using SkiaSharp;

namespace Pos.Infrastructure.Products;

/// <summary>
/// Valida y optimiza imágenes de producto con SkiaSharp, la misma biblioteca que usa Avalonia
/// (003, research §6). El formato se identifica por la firma de los bytes, nunca por la extensión.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>Límite de píxeles antes de decodificar, contra imágenes diseñadas para agotar la memoria.</summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>Límite de cualquiera de los lados antes de decodificar.</summary>
    public const int MaxSide = 20_000;

    /// <summary>Calidad WEBP de la imagen optimizada y de la miniatura.</summary>
    public const int Quality = 80;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public ImageProcessingResult Process(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = ReadAtMost(content, Product.ImageMaxBytes);
        if (bytes is null)
        {
            return ImageProcessingResult.Failure(InvalidImageReason.TooLarge);
        }

        if (!HasSupportedSignature(bytes))
        {
            return ImageProcessingResult.Failure(InvalidImageReason.UnsupportedFormat);
        }

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return ImageProcessingResult.Failure(InvalidImageReason.Corrupt);
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0)
        {
            return ImageProcessingResult.Failure(InvalidImageReason.Corrupt);
        }

        if (info.Width > MaxSide || info.Height > MaxSide || (long)info.Width * info.Height > MaxPixels)
        {
            return ImageProcessingResult.Failure(InvalidImageReason.DimensionsTooLarge);
        }

        using var decoded = Decode(codec);
        if (decoded is null)
        {
            return ImageProcessingResult.Failure(InvalidImageReason.Corrupt);
        }

        using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
        using var image = ResizeToFit(oriented, Product.ImageMaxSide);
        using var thumbnail = ResizeToFit(oriented, Product.ThumbnailMaxSide);

        return ImageProcessingResult.Success(EncodeWebp(image), EncodeWebp(thumbnail), image.Width, image.Height);
    }

    /// <summary>Lee todo el flujo si no excede el límite; devuelve nulo si lo excede.</summary>
    private static byte[]? ReadAtMost(Stream content, long limit)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>JPEG <c>FF D8 FF</c>, PNG <c>89 50 4E 47 0D 0A 1A 0A</c> o WEBP <c>RIFF....WEBP</c>.</summary>
    private static bool HasSupportedSignature(byte[] bytes)
    {
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReadOnlySpan<byte> riff = "RIFF"u8;
        ReadOnlySpan<byte> webp = "WEBP"u8;

        var span = bytes.AsSpan();
        return span.StartsWith(jpeg)
            || span.StartsWith(png)
            || (span.Length >= 12 && span.StartsWith(riff) && span[8..12].SequenceEqual(webp));
    }

    /// <summary>
    /// Decodifica el primer cuadro (en un WEBP animado, solo ese). Si la imagen es mucho mayor que
    /// el destino, pide al códec una escala reducida para no ocupar memoria de más.
    /// </summary>
    private static SKBitmap? Decode(SKCodec codec)
    {
        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var scale = longest > Product.ImageMaxSide * 2 ? (float)(Product.ImageMaxSide * 2) / longest : 1f;
        var size = codec.GetScaledDimensions(scale);
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        var bitmap = new SKBitmap(info);
        // Un archivo truncado (IncompleteInput) se trata como dañado: no se guarda una imagen a medias.
        if (codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    /// <summary>Aplica la orientación EXIF (fotos de teléfono) para que la imagen quede derecha.</summary>
    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swaps ? source.Height : source.Width;
        var height = swaps ? source.Width : source.Height;

        var rotated = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(rotated);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(OriginMatrix(origin, source.Width, source.Height));
        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }

    private static SKMatrix OriginMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    /// <summary>Reduce sin deformar para que el lado mayor no exceda <paramref name="maxSide"/>; nunca amplía.</summary>
    private static SKBitmap ResizeToFit(SKBitmap source, int maxSide)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxSide)
        {
            return source.Copy();
        }

        var ratio = (double)maxSide / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));
        return source.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), Sampling);
    }

    /// <summary>WEBP con pérdida y canal alfa: conserva la transparencia de PNG y WEBP.</summary>
    private static byte[] EncodeWebp(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return encoded.ToArray();
    }
}
