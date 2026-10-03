using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Scanner.InspectScan;
using Pos.Desktop.Common;
using Pos.Desktop.Common.Scanner;
using Pos.Desktop.Resources;
using Pos.Domain.Products;

namespace Pos.Desktop.Settings;

/// <summary>Una lectura en la pantalla "Probar escáner", ya con sus textos para mostrar.</summary>
public sealed record ScanTestItem(
    string ReceivedAt,
    string Text,
    string Format,
    string Length,
    string Terminator,
    bool EndedWithEnter,
    string Speed,
    string Product);

/// <summary>
/// Pantalla "Ayuda > Probar escáner" (021, FR-015 a FR-019): muestra cada lectura con su formato, largo,
/// terminador, velocidad y producto, y las últimas 10. Las lecturas viven solo aquí (la página es de la
/// sesión y se pierden al cerrarla); no tiene acceso a la venta ni al borrador.
/// </summary>
public sealed partial class ScannerTestViewModel : PageViewModel
{
    public const int HistoryLimit = 10;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IClock _clock;

    public ScannerTestViewModel(UseCases useCases, OperationRunner runner, IClock clock)
    {
        _useCases = useCases;
        _runner = runner;
        _clock = clock;
    }

    public override string Title => Strings.Nav_ScannerTest;

    /// <summary>La lectura más reciente, o nula.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    public partial ScanTestItem? Current { get; private set; }

    public bool HasCurrent => Current is not null;

    /// <summary>Últimas lecturas, de la más reciente a la más antigua (FR-018).</summary>
    public ObservableCollection<ScanTestItem> History { get; } = [];

    /// <summary>Inspecciona una lectura que la vista ya cerró con su terminador o por silencio.</summary>
    public async Task ReceiveAsync(ScanReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var (completed, result) = await _runner.RunAsync(
            "InspeccionarLectura",
            () => _useCases.RunAsync<InspectScanHandler, Result<ScanInspectionDto>>(
                h => h.HandleAsync(new InspectScanQuery(reading.RawText), CancellationToken.None)),
            new Dictionary<string, object?> { ["Length"] = reading.RawText.Length, ["Terminator"] = reading.Terminator });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var item = ToItem(reading, result.Value);
        Current = item;
        History.Insert(0, item);
        while (History.Count > HistoryLimit)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        History.Clear();
        Current = null;
    }

    private ScanTestItem ToItem(ScanReading reading, ScanInspectionDto inspection) => new(
        _clock.UtcNow.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture),
        ScanTextFormatter.ToVisible(reading.RawText),
        FormatText(inspection.Format),
        inspection.Length.ToString(CultureInfo.CurrentCulture),
        reading.Terminator switch
        {
            Terminator.Enter => Strings.ScannerTest_EndEnter,
            Terminator.Tab => $"{Strings.ScannerTest_EndTab}. {Strings.ScannerTest_ConfigureEnter}",
            _ => $"{Strings.ScannerTest_EndNone}. {Strings.ScannerTest_ConfigureEnter}",
        },
        reading.Terminator == Terminator.Enter,
        reading.IsBurst ? Strings.ScannerTest_SpeedScanner : Strings.ScannerTest_SpeedManual,
        inspection.Product switch
        {
            null => Strings.ScannerTest_NoProduct,
            { IsActive: true } p => $"{p.Name} · SKU {p.Sku}",
            var p => $"{p.Name} · SKU {p.Sku} {Strings.ScannerTest_Inactive}",
        });

    private static string FormatText(BarcodeFormat format) => format switch
    {
        BarcodeFormat.Ean13 => Strings.ScannerTest_FormatEan13,
        BarcodeFormat.Ean8 => Strings.ScannerTest_FormatEan8,
        BarcodeFormat.Code128OrCode39 => Strings.ScannerTest_FormatCode,
        _ => Strings.ScannerTest_FormatUnrecognized,
    };
}
