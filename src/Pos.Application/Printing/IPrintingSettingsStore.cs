namespace Pos.Application.Printing;

/// <summary>Configuración de impresión por máquina; sin archivo o dañado devuelve los valores predeterminados.</summary>
public interface IPrintingSettingsStore
{
    PrintingSettings Load();

    void Save(PrintingSettings settings);
}
