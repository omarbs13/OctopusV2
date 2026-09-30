using Pos.Application.Abstractions;

namespace Pos.Application.Printing.SavePrintingSettings;

/// <summary>Guarda la configuración local de impresión. Sin impresora con impresión automática no es un error.</summary>
public sealed class SavePrintingSettingsHandler
{
    private readonly IPrintingSettingsStore _store;

    public SavePrintingSettingsHandler(IPrintingSettingsStore store) => _store = store;

    public Result Handle(PrintingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var name = string.IsNullOrWhiteSpace(settings.PrinterName) ? null : settings.PrinterName.Trim();
        _store.Save(settings with { PrinterName = name });
        return Result.Success();
    }
}
