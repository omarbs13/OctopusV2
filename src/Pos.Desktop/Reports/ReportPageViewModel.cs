using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Desktop.Common;
using Pos.Domain.Reports;

namespace Pos.Desktop.Reports;

/// <summary>
/// Base de las pantallas de reporte: selector de período, gráfica, indicador de carga y estado vacío
/// "Sin datos en este período". Se consulta al activar la pantalla y al cambiar el período o un filtro
/// (contracts/ui.md).
/// </summary>
public abstract partial class ReportPageViewModel : PageViewModel
{
    private int _loadVersion;

    private readonly ReportExportCoordinator _exporter;

    protected ReportPageViewModel(OperationRunner runner, IChartRenderer renderer, ReportExportCoordinator exporter, ReportPreset initialPreset)
    {
        Runner = runner;
        _exporter = exporter;
        Picker = new PeriodPickerViewModel(initialPreset);
        Picker.PeriodChanged += (_, _) => RestartLoad();
        Chart = new ChartViewModel(renderer);
    }

    public PeriodPickerViewModel Picker { get; }

    public ChartViewModel Chart { get; }

    protected OperationRunner Runner { get; }

    /// <summary>Nombre de la operación para el registro de errores.</summary>
    protected abstract string OperationName { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    public partial bool IsBusy { get; protected set; }

    /// <summary>Se está generando un archivo; el resto de la aplicación sigue usable.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    public partial bool IsExporting { get; private set; }

    /// <summary>Verdadero cuando la consulta no devolvió datos: se muestra "Sin datos en este período".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    public partial bool IsEmpty { get; protected set; }

    public bool HasData => !IsEmpty;

    /// <summary>Parámetros del reporte tal como se ven (período, filtros, orden); la exportación no pagina.</summary>
    protected abstract ExportRequest CreateExportRequest(ExportFormat format);

    public override Task OnActivatedAsync() => ReloadAsync();

    private bool CanExport() => HasData && !IsBusy && !IsExporting;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportPdfAsync() => ExportAsync(ExportFormat.Pdf);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportExcelAsync() => ExportAsync(ExportFormat.Xlsx);

    private async Task ExportAsync(ExportFormat format)
    {
        IsExporting = true;
        try
        {
            await _exporter.ExportAsync(CreateExportRequest(format), Picker.PeriodText);
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>Vuelve a consultar con los criterios actuales.</summary>
    public async Task ReloadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        IsBusy = true;
        try
        {
            await Runner.RunAsync(OperationName, () => LoadAsync(() => version == _loadVersion));
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>Consulta y llena la pantalla; <paramref name="isCurrent"/> indica si aún es la consulta más reciente.</summary>
    protected abstract Task LoadAsync(Func<bool> isCurrent);

    protected void RestartLoad() => _ = ReloadAsync();
}
