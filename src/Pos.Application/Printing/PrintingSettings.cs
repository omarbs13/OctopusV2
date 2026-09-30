namespace Pos.Application.Printing;

public enum PaperWidth
{
    Mm58,
    Mm80,
}

/// <summary>
/// Configuración de impresión de esta máquina (006, FR-006). Vive en las preferencias locales, no en
/// la base de datos. Guardar la impresión automática sin impresora no es un error.
/// </summary>
public sealed record PrintingSettings(
    string? PrinterName = null,
    bool UseVirtualPrinter = false,
    PaperWidth PaperWidth = PaperWidth.Mm80,
    bool AutoPrint = false,
    bool AutoOpenDrawer = true)
{
    /// <summary>Columnas de texto del papel: 32 en 58 mm y 48 en 80 mm.</summary>
    public int Columns => PaperWidth == PaperWidth.Mm58 ? 32 : 48;

    /// <summary>Ancho máximo del logotipo en puntos de impresión.</summary>
    public int LogoMaxDots => PaperWidth == PaperWidth.Mm58 ? 384 : 576;

    /// <summary>Hay una impresora (real o virtual) a la cual enviar.</summary>
    public bool IsConfigured => UseVirtualPrinter || !string.IsNullOrWhiteSpace(PrinterName);
}
