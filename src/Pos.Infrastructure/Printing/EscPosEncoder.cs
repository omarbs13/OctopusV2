using System.Text;
using Pos.Application.Printing.Ticket;
using SkiaSharp;

namespace Pos.Infrastructure.Printing;

/// <summary>
/// Codifica el ticket al subconjunto ESC/POS que se necesita (006, research §4): inicializar, tabla
/// CP858, alineación, negrita, logotipo raster, avance y corte, y el pulso del cajón.
/// </summary>
public static class EscPosEncoder
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;
    private const byte LineFeed = 0x0A;

    /// <summary>Número de la tabla CP858 en <c>ESC t n</c>.</summary>
    private const byte Cp858Table = 19;

    /// <summary>Umbral de luminancia (0 a 255) bajo el cual un punto se imprime negro.</summary>
    private const int BlackThreshold = 160;

    private static readonly Lazy<Encoding> Cp858 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(858, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    });

    /// <summary>Pulso del cajón en el pin 2: <c>ESC p 0 25 250</c>.</summary>
    public static byte[] DrawerPulse() => [Esc, 0x70, 0x00, 25, 250];

    public static byte[] Encode(TicketDocument ticket, int logoMaxDots)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        using var output = new MemoryStream();
        output.Write([Esc, 0x40]);
        output.Write([Esc, 0x74, Cp858Table]);

        if (ticket.Logo is { Length: > 0 } logo && Rasterize(logo, logoMaxDots) is { } raster)
        {
            output.Write([Esc, 0x61, 1]);
            output.Write(raster);
        }

        foreach (var line in ticket.Lines)
        {
            output.Write([Esc, 0x61, line.Alignment switch
            {
                TicketAlignment.Center => 1,
                TicketAlignment.Right => 2,
                _ => 0,
            }]);
            output.Write([Esc, 0x45, line.Bold ? (byte)1 : (byte)0]);
            output.Write(Cp858.Value.GetBytes(line.Text));
            output.WriteByte(LineFeed);
        }

        output.Write([Esc, 0x45, 0]);
        output.Write([Esc, 0x61, 0]);
        output.Write([LineFeed, LineFeed, LineFeed, LineFeed]);
        output.Write([Gs, 0x56, 66, 0]);
        return output.ToArray();
    }

    /// <summary>Imagen monocromo <c>GS v 0</c>; nulo si no se puede decodificar (el ticket sale sin logotipo).</summary>
    private static byte[]? Rasterize(byte[] image, int maxDots)
    {
        using var decoded = SKBitmap.Decode(image);
        if (decoded is null || decoded.Width < 1 || decoded.Height < 1)
        {
            return null;
        }

        var width = Math.Min(decoded.Width, maxDots) / 8 * 8;
        if (width < 8)
        {
            return null;
        }

        var height = Math.Max(1, (int)Math.Round((double)decoded.Height * width / decoded.Width));
        if (height > 0xFFFF)
        {
            return null;
        }

        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(decoded, new SKRect(0, 0, width, height), paint);
        }

        var bytesPerRow = width / 8;
        var data = new byte[bytesPerRow * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = resized.GetPixel(x, y);
                var luminance = ((pixel.Red * 299) + (pixel.Green * 587) + (pixel.Blue * 114)) / 1000;
                if (luminance < BlackThreshold)
                {
                    data[(y * bytesPerRow) + (x / 8)] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        var header = new byte[] { Gs, 0x76, 0x30, 0x00, (byte)(bytesPerRow & 0xFF), (byte)(bytesPerRow >> 8), (byte)(height & 0xFF), (byte)(height >> 8) };
        return [.. header, .. data];
    }
}
