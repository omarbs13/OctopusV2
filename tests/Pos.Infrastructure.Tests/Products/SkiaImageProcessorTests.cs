using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using SkiaSharp;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Validación y optimización de imágenes (003, FR-022 a FR-024; SC-005). Imágenes generadas en memoria.</summary>
public sealed class SkiaImageProcessorTests
{
    private static readonly SkiaImageProcessor Processor = new();

    private static ImageProcessingResult Process(byte[] bytes) => Processor.Process(new MemoryStream(bytes));

    /// <summary>Imagen de dos colores: mitad izquierda roja y mitad derecha azul.</summary>
    private static byte[] Encode(int width, int height, SKEncodedImageFormat format, bool transparentRight = false)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = transparentRight ? SKColors.Transparent : SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static SKBitmap Decode(byte[] bytes) => SKBitmap.Decode(bytes);

    /// <summary>Inserta un segmento EXIF (APP1) con la orientación indicada justo después del SOI.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] payload =
        [
            .. "Exif\0\0"u8,
            .. "MM\0*"u8, 0, 0, 0, 8, // TIFF big-endian, IFD en el byte 8
            0, 1, // una entrada
            0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0, // Orientation, SHORT
            0, 0, 0, 0, // sin más IFD
        ];
        var length = payload.Length + 2;
        return [.. jpeg[..2], 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, .. jpeg[2..]];
    }

    private static void AssertWebp(byte[]? bytes)
    {
        Assert.NotNull(bytes);
        Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
        Assert.Equal("WEBP"u8.ToArray(), bytes[8..12]);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void FormatosAdmitidos_ProducenWebpConMiniatura(SKEncodedImageFormat format)
    {
        var result = Process(Encode(400, 300, format));

        Assert.Null(result.Error);
        AssertWebp(result.Content);
        AssertWebp(result.Thumbnail);
        Assert.Equal((400, 300), (result.Width, result.Height));
    }

    [Fact]
    public void ImagenGrande_SeReduceA800YMiniaturaA128SinDeformar()
    {
        var result = Process(Encode(3000, 2000, SKEncodedImageFormat.Jpeg));

        Assert.Equal((800, 533), (result.Width, result.Height));
        using var content = Decode(result.Content!);
        using var thumbnail = Decode(result.Thumbnail!);
        Assert.Equal((800, 533), (content.Width, content.Height));
        Assert.Equal((128, 85), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void ImagenPequena_NoSeAmplia()
    {
        var result = Process(Encode(100, 50, SKEncodedImageFormat.Png));

        Assert.Equal((100, 50), (result.Width, result.Height));
        using var thumbnail = Decode(result.Thumbnail!);
        Assert.Equal((100, 50), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void PngConTransparencia_ConservaElAlfa()
    {
        var result = Process(Encode(200, 100, SKEncodedImageFormat.Png, transparentRight: true));

        using var content = Decode(result.Content!);
        Assert.True(content.GetPixel(20, 50).Alpha > 200);
        Assert.True(content.GetPixel(180, 50).Alpha < 20);
    }

    [Fact]
    public void JpegConOrientacionExif6_QuedaRotadoALaDerecha()
    {
        // 300 x 100 con la mitad izquierda roja; rotada 90° a la derecha queda de 100 x 300 con el rojo arriba.
        var result = Process(WithExifOrientation(Encode(300, 100, SKEncodedImageFormat.Jpeg), 6));

        Assert.Equal((100, 300), (result.Width, result.Height));
        using var content = Decode(result.Content!);
        var top = content.GetPixel(50, 30);
        var bottom = content.GetPixel(50, 270);
        Assert.True(top.Red > 200 && top.Blue < 60, $"Arriba debería ser rojo: {top}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 60, $"Abajo debería ser azul: {bottom}");
    }

    [Fact]
    public void Gif_NoEsAdmitido()
    {
        Assert.Equal(InvalidImageReason.UnsupportedFormat, Process([.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0]).Error);
    }

    [Fact]
    public void TextoConExtensionDeImagen_NoEsAdmitido()
    {
        Assert.Equal(InvalidImageReason.UnsupportedFormat, Process("Esto no es una imagen .jpg"u8.ToArray()).Error);
    }

    [Fact]
    public void JpegTruncado_EstaDaniado()
    {
        var jpeg = Encode(400, 300, SKEncodedImageFormat.Jpeg);

        Assert.Equal(InvalidImageReason.Corrupt, Process(jpeg[..(jpeg.Length / 3)]).Error);
    }

    [Fact]
    public void CabeceraWebpConContenidoBasura_EstaDaniada()
    {
        byte[] bytes = [.. "RIFF"u8, 40, 0, 0, 0, .. "WEBP"u8, .. Enumerable.Repeat((byte)0xAB, 40)];

        Assert.Equal(InvalidImageReason.Corrupt, Process(bytes).Error);
    }

    [Fact]
    public void LadoMayorA20000_DimensionesDemasiadoGrandes()
    {
        Assert.Equal(InvalidImageReason.DimensionsTooLarge, Process(Encode(25_000, 10, SKEncodedImageFormat.Png)).Error);
    }

    [Fact]
    public void FlujoMayorA5MB_DemasiadoGrande()
    {
        var bytes = new byte[Product.ImageMaxBytes + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        Assert.Equal(InvalidImageReason.TooLarge, Process(bytes).Error);
    }
}
