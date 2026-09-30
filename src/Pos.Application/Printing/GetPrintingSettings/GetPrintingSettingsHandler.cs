namespace Pos.Application.Printing.GetPrintingSettings;

public sealed class GetPrintingSettingsHandler
{
    private readonly IPrintingSettingsStore _store;

    public GetPrintingSettingsHandler(IPrintingSettingsStore store) => _store = store;

    public PrintingSettings Handle() => _store.Load();
}
