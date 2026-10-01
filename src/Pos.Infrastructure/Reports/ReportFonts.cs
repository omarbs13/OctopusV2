using SkiaSharp;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Tipografías de gráficas y PDF con lista de respaldo (research §6): la primera familia instalada que
/// tenga acentos y eñe; si ninguna, la tipografía predeterminada de Skia.
/// </summary>
internal static class ReportFonts
{
    private static readonly string[] Families = ["Inter", "Segoe UI", "DejaVu Sans", "Liberation Sans", "Arial"];

    public static SKTypeface Regular { get; } = Resolve(SKFontStyle.Normal);

    public static SKTypeface Bold { get; } = Resolve(SKFontStyle.Bold);

    public static SKFont Font(float size, bool bold = false) =>
        new(bold ? Bold : Regular, size) { Edging = SKFontEdging.SubpixelAntialias };

    private static SKTypeface Resolve(SKFontStyle style)
    {
        foreach (var family in Families)
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null
                && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase)
                && typeface.ContainsGlyph('ñ')
                && typeface.ContainsGlyph('é'))
            {
                return typeface;
            }
        }

        return SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
    }
}
