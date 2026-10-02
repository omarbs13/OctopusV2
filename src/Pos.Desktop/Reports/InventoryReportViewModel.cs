using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Inventory;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Desktop.Categories;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Inventory;
using Pos.Domain.Reports;

namespace Pos.Desktop.Reports;

/// <summary>Opción del filtro de estado de la tabla de inventario.</summary>
public sealed record InventoryStatusOption(StockFilter Filter, string Label);

/// <summary>Fila de la tabla de inventario con los textos ya formateados.</summary>
public sealed record InventoryReportRowItem(InventoryReportRow Row)
{
    public string Name => Row.Name;

    public string Sku => Row.Sku;

    public string OnHandText => QuantityConverter.Format(Row.OnHandThousandths, Row.DecimalPlaces);

    public string MinimumText => QuantityConverter.FormatOrDash(Row.MinimumThousandths, Row.DecimalPlaces);

    public string ReorderPointText => QuantityConverter.FormatOrDash(Row.ReorderPointThousandths, Row.DecimalPlaces);

    public string Unit => Row.UnitName;

    /// <summary>"Sin categoría" si no tiene; "(inactiva)" si corresponde (016, FR-020).</summary>
    public string Category => CategoryMessages.Display(Row.CategoryName, Row.CategoryIsActive);

    public string StatusText => Row.Status switch
    {
        StockStatus.Low => Strings.Stock_StatusLow,
        StockStatus.Out => Strings.Stock_StatusOut,
        _ => Strings.Stock_StatusNormal,
    };
}

/// <summary>
/// Reportes > Inventario: estado del inventario al cierre de una fecha (Historia 3). Tarjetas y gráfica
/// usan todos los productos de la categoría elegida (016); el filtro de estado, la búsqueda y el orden solo
/// afectan a la tabla. Recibe un <see cref="StockFilter"/> por navegación desde las notificaciones y la
/// tarjeta de alertas de existencia (022).
/// </summary>
public sealed partial class InventoryReportViewModel : ReportPageViewModel, INavigationArgumentReceiver
{
    private const string NormalColor = "#2E7D32";
    private const string LowColor = "#EF6C00";
    private const string OutColor = "#C62828";
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private readonly UseCases _useCases;
    private int _searchVersion;
    private bool _suppressReload;

    public InventoryReportViewModel(UseCases useCases, OperationRunner runner, IChartRenderer renderer, ReportExportCoordinator exporter)
        : base(runner, renderer, exporter, ReportPreset.Today)
    {
        _useCases = useCases;
        CategoryFilter = new CategoryPickerViewModel(useCases, runner, CategoryPickerMode.Filter);
        CategoryFilter.SelectionChanged += (_, _) => RestartFromFirstPage();
        StatusOptions =
        [
            new(StockFilter.All, Strings.Stock_FilterAll),
            new(StockFilter.Normal, Strings.Stock_FilterNormal),
            new(StockFilter.Low, Strings.Stock_FilterLow),
            new(StockFilter.Out, Strings.Stock_FilterOut),
            new(StockFilter.Alert, Strings.Stock_FilterAlert),
            new(StockFilter.Urgent, Strings.Stock_FilterUrgent),
        ];
        SelectedStatus = StatusOptions[0];
    }

    public override string Title => Strings.Nav_ReportInventory;

    protected override string OperationName => "ReporteInventario";

    public IReadOnlyList<InventoryStatusOption> StatusOptions { get; }

    public ObservableCollection<InventoryReportRowItem> Rows { get; } = [];

    [ObservableProperty]
    public partial InventoryStatusOption SelectedStatus { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AsOfText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string LowText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OutText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool NoMatches { get; private set; }

    [ObservableProperty]
    public partial InventoryReportSort SortColumn { get; private set; } = InventoryReportSort.Name;

    [ObservableProperty]
    public partial bool SortDescending { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalRows { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Reports_PageSummary, Rows.Count, TotalRows, CurrentPage, TotalPages);

    /// <summary>Filtro "Categoría" (016, FR-013): limita tarjetas, gráfica y tabla.</summary>
    public CategoryPickerViewModel CategoryFilter { get; }

    public string CategoryHeader => Header(InventoryReportSort.Category, Strings.Category_ColCategory);

    public string NameHeader => Header(InventoryReportSort.Name, Strings.Reports_Inventory_ColName);

    public string SkuHeader => Header(InventoryReportSort.Sku, Strings.Reports_Inventory_ColSku);

    public string OnHandHeader => Header(InventoryReportSort.OnHand, Strings.Reports_Inventory_ColOnHand);

    public override async Task OnActivatedAsync()
    {
        await CategoryFilter.LoadAsync();
        await ReloadAsync();
    }

    /// <summary>
    /// Con un <see cref="StockFilter"/>: período "Hoy", ese filtro, sin búsqueda y en la página 1. La
    /// recarga la hace <see cref="OnActivatedAsync"/>, que el navegador llama después (022, contracts/ui.md).
    /// </summary>
    public void Receive(object argument)
    {
        if (argument is not StockFilter filter)
        {
            return;
        }

        Interlocked.Increment(ref _searchVersion);
        _suppressReload = true;
        try
        {
            Picker.SelectPresetSilently(ReportPreset.Today);
            SelectedStatus = StatusOptions.First(o => o.Filter == filter);
            SearchText = string.Empty;
            CurrentPage = 1;
        }
        finally
        {
            _suppressReload = false;
        }
    }

    partial void OnSelectedStatusChanged(InventoryStatusOption value)
    {
        if (!_suppressReload)
        {
            RestartFromFirstPage();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!_suppressReload)
        {
            _ = SearchAfterDelayAsync();
        }
    }

    [RelayCommand]
    private void SortBy(InventoryReportSort column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = false;
        }

        OnPropertyChanged(nameof(NameHeader));
        OnPropertyChanged(nameof(CategoryHeader));
        OnPropertyChanged(nameof(SkuHeader));
        OnPropertyChanged(nameof(OnHandHeader));
        RestartFromFirstPage();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    protected override async Task LoadAsync(Func<bool> isCurrent)
    {
        var query = BuildQuery();
        var result = await _useCases.RunAsync<GetInventoryReportHandler, Result<InventoryReport>>(
            h => h.HandleAsync(query, CancellationToken.None));
        if (!isCurrent() || !result.IsSuccess)
        {
            return;
        }

        Apply(result.Value);
    }

    protected override ExportRequest CreateExportRequest(ExportFormat format) => ExportRequest.ForInventory(BuildQuery(), format);

    private InventoryReportQuery BuildQuery() => new(
        Picker.Period.ToDate,
        SelectedStatus.Filter,
        SearchText,
        SortColumn,
        SortDescending,
        CurrentPage,
        Category: CategoryFilter.Filter);

    private void Apply(InventoryReport report)
    {
        var counts = report.Counts;
        IsEmpty = counts.Total == 0;
        AsOfText = string.Format(CultureInfo.CurrentCulture, Strings.Reports_Inventory_AsOf, Picker.Period.ToDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        TotalText = counts.Total.ToString("N0", CultureInfo.CurrentCulture);
        ActiveText = counts.Active.ToString("N0", CultureInfo.CurrentCulture);
        LowText = counts.Low.ToString("N0", CultureInfo.CurrentCulture);
        OutText = counts.Out.ToString("N0", CultureInfo.CurrentCulture);

        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new InventoryReportRowItem(row));
        }

        NoMatches = !IsEmpty && report.Rows.Count == 0;
        TotalRows = report.TotalRows;
        TotalPages = report.TotalPages;
        CurrentPage = report.Page;
        OnPropertyChanged(nameof(PageSummary));

        Chart.Set(IsEmpty
            ? null
            : new ChartSpec(
                ChartKind.Pie,
                Strings.Reports_Inventory_ChartTitle,
                [
                    new ChartPoint(Strings.Reports_Inventory_Normal, counts.Normal, NormalColor),
                    new ChartPoint(Strings.Stock_StatusLow, counts.Low, LowColor),
                    new ChartPoint(Strings.Stock_StatusOut, counts.Out, OutColor),
                ],
                ChartValueFormat.Count));
    }

    private string Header(InventoryReportSort column, string label) =>
        SortColumn == column ? $"{label} {(SortDescending ? "▼" : "▲")}" : label;

    private void RestartFromFirstPage()
    {
        CurrentPage = 1;
        RestartLoad();
    }

    private async Task SearchAfterDelayAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        await Task.Delay(SearchDelay);
        if (version == _searchVersion)
        {
            RestartFromFirstPage();
        }
    }

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return ReloadAsync();
    }
}
