using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Printing;
using Pos.Application.Printing.GetPrintingSettings;
using Pos.Application.Printing.ListPrinters;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Printing.SavePrintingSettings;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Settings;

/// <summary>Opción del selector de impresora: una del sistema o la impresora virtual.</summary>
public sealed record PrinterChoice(string? Name, string Label)
{
    public bool IsVirtual => Name is null;
}

/// <summary>Pantalla "Impresora": destino, ancho de papel, opciones automáticas e impresión de prueba (006, US2).</summary>
public sealed partial class PrinterSettingsViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly TicketPrintingService _printing;

    public PrinterSettingsViewModel(UseCases useCases, OperationRunner runner, TicketPrintingService printing)
    {
        _useCases = useCases;
        _runner = runner;
        _printing = printing;
    }

    public override string Title => Strings.Nav_Printer;

    public ObservableCollection<PrinterChoice> Printers { get; } = [];

    public IReadOnlyList<string> PaperWidths { get; } = [Strings.Printer_Paper58, Strings.Printer_Paper80];

    [ObservableProperty]
    public partial PrinterChoice? SelectedPrinter { get; set; }

    [ObservableProperty]
    public partial int PaperWidthIndex { get; set; } = 1;

    [ObservableProperty]
    public partial bool AutoPrint { get; set; }

    [ObservableProperty]
    public partial bool AutoOpenDrawer { get; set; } = true;

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(TestPrintCommand))]
    public partial bool IsBusy { get; private set; }

    public override async Task OnActivatedAsync()
    {
        StatusMessage = null;
        var (catalogOk, printersResult) = await _runner.RunAsync(
            "ListarImpresoras",
            () => _useCases.RunAsync<ListPrintersHandler, Result<IReadOnlyList<string>>>(h => h.HandleAsync(CancellationToken.None)));
        var printers = printersResult?.IsSuccess == true ? printersResult.Value : null;
        var (settingsOk, settings) = await _runner.RunAsync(
            "CargarConfiguracionImpresion",
            () => _useCases.RunAsync<GetPrintingSettingsHandler, PrintingSettings>(h => Task.FromResult(h.Handle())));
        if (!catalogOk || !settingsOk || settings is null)
        {
            return;
        }

        Printers.Clear();
        Printers.Add(new PrinterChoice(null, Strings.Printer_Virtual));
        foreach (var name in printers ?? [])
        {
            Printers.Add(new PrinterChoice(name, name));
        }

        // Una impresora guardada que ya no aparece se conserva en la lista para no perder la configuración.
        if (!settings.UseVirtualPrinter && settings.PrinterName is { } saved && Printers.All(p => p.Name != saved))
        {
            Printers.Add(new PrinterChoice(saved, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Printer_NotFound, saved)));
        }

        SelectedPrinter = settings.UseVirtualPrinter
            ? Printers[0]
            : Printers.FirstOrDefault(p => p.Name == settings.PrinterName);
        PaperWidthIndex = settings.PaperWidth == PaperWidth.Mm58 ? 0 : 1;
        AutoPrint = settings.AutoPrint;
        AutoOpenDrawer = settings.AutoOpenDrawer;
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (!await PersistAsync())
            {
                return;
            }

            StatusMessage = Strings.Printer_Saved;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Guarda la configuración en pantalla y envía el ticket de prueba.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task TestPrintAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            if (!await PersistAsync())
            {
                return;
            }

            await _printing.PrintAsync(PrintSource.Sample, isReprint: false, Strings.Printer_TestLabel, automatic: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsIdle() => !IsBusy;

    private async Task<bool> PersistAsync()
    {
        var settings = new PrintingSettings(
            SelectedPrinter?.Name,
            UseVirtualPrinter: SelectedPrinter?.IsVirtual ?? false,
            PaperWidthIndex == 0 ? PaperWidth.Mm58 : PaperWidth.Mm80,
            AutoPrint,
            AutoOpenDrawer);
        var (completed, result) = await _runner.RunAsync(
            "GuardarConfiguracionImpresion",
            () => _useCases.RunAsync<SavePrintingSettingsHandler, Result>(h => h.HandleAsync(settings, CancellationToken.None)));
        return completed && result is { IsSuccess: true };
    }
}
