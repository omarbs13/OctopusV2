using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Reports;

namespace Pos.Desktop.Reports;

/// <summary>Fila de la tabla de detalle con los textos ya formateados.</summary>
public sealed record SalesReportRowItem(SalesReportRow Row)
{
    public string Folio => Row.FolioText;

    public string DateText => Row.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string Cashier => Row.CashierName;

    public string TotalText => MoneyConverter.Format(Row.TotalCents);
}

/// <summary>Reportes > Ventas: tarjetas, gráfica por día y tabla de detalle de un período (Historia 1).</summary>
public sealed partial class SalesReportViewModel : ReportPageViewModel
{
    private readonly UseCases _useCases;
    private bool _suppress;

    public SalesReportViewModel(UseCases useCases, OperationRunner runner, IChartRenderer renderer, ReportExportCoordinator exporter)
        : base(runner, renderer, exporter, ReportPreset.Today)
    {
        _useCases = useCases;
        CashierOptions = [new CashierOption(null, Strings.Reports_CashierAll)];
        _suppress = true;
        SelectedCashier = CashierOptions[0];
        _suppress = false;
    }

    public override string Title => Strings.Nav_ReportSales;

    protected override string OperationName => "ReporteVentas";

    public ObservableCollection<CashierOption> CashierOptions { get; }

    public ObservableCollection<SalesReportRowItem> Rows { get; } = [];

    [ObservableProperty]
    public partial CashierOption SelectedCashier { get; set; }

    [ObservableProperty]
    public partial bool Compare { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AverageText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CashText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CardText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TransferText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OnAccountText { get; private set; } = string.Empty;

    /// <summary>"Total descontado" del período (015, FR-018).</summary>
    [ObservableProperty]
    public partial string DiscountedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasComparison { get; private set; }

    [ObservableProperty]
    public partial string PreviousText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string VariationText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial SalesReportSort SortColumn { get; private set; } = SalesReportSort.Date;

    [ObservableProperty]
    public partial bool SortDescending { get; private set; } = true;

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

    public string FolioHeader => Header(SalesReportSort.Folio, Strings.Reports_Sales_ColFolio);

    public string DateHeader => Header(SalesReportSort.Date, Strings.Reports_Sales_ColDate);

    public string CashierHeader => Header(SalesReportSort.Cashier, Strings.Reports_Sales_ColCashier);

    public string TotalHeader => Header(SalesReportSort.Total, Strings.Reports_Sales_ColTotal);

    public override async Task OnActivatedAsync()
    {
        await LoadCashiersAsync();
        await ReloadAsync();
    }

    partial void OnSelectedCashierChanged(CashierOption value) => RestartFromFirstPage();

    partial void OnCompareChanged(bool value) => RestartFromFirstPage();

    [RelayCommand]
    private void SortBy(SalesReportSort column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = column is SalesReportSort.Date or SalesReportSort.Total;
        }

        OnPropertyChanged(nameof(FolioHeader));
        OnPropertyChanged(nameof(DateHeader));
        OnPropertyChanged(nameof(CashierHeader));
        OnPropertyChanged(nameof(TotalHeader));
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
        var result = await _useCases.RunAsync<GetSalesReportHandler, Result<SalesReport>>(
            h => h.HandleAsync(query, CancellationToken.None));
        if (!isCurrent() || !result.IsSuccess)
        {
            return;
        }

        Apply(result.Value);
    }

    protected override ExportRequest CreateExportRequest(ExportFormat format) => ExportRequest.ForSales(BuildQuery(), format);

    private SalesReportQuery BuildQuery() => new(
        Picker.Period,
        SelectedCashier.UserId,
        Compare,
        SortColumn,
        SortDescending,
        CurrentPage);

    private void Apply(SalesReport report)
    {
        IsEmpty = report.Totals.SalesCount == 0;
        var totals = report.Totals;
        TotalText = MoneyConverter.Format(totals.TotalCents);
        CountText = totals.SalesCount.ToString("N0", CultureInfo.CurrentCulture);
        AverageText = MoneyConverter.Format(totals.AverageTicketCents);
        CashText = MoneyConverter.Format(totals.CashCents);
        CardText = MoneyConverter.Format(totals.CardCents);
        TransferText = MoneyConverter.Format(totals.TransferCents);
        OnAccountText = MoneyConverter.Format(totals.OnAccountCents);
        DiscountedText = MoneyConverter.Format(totals.DiscountCents);

        HasComparison = report.Comparison is not null;
        if (report.Comparison is { } comparison)
        {
            PreviousText = MoneyConverter.Format(comparison.Previous.TotalCents);
            VariationText = FormatVariation(comparison.VariationBasisPoints);
        }

        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new SalesReportRowItem(row));
        }

        TotalRows = report.TotalRows;
        TotalPages = report.TotalPages;
        OnPropertyChanged(nameof(PageSummary));

        Chart.Set(IsEmpty
            ? null
            : new ChartSpec(
                ChartKind.Line,
                Strings.Reports_Sales_ChartTitle,
                [.. report.Days.Select(d => new ChartPoint(d.LocalDate.ToString("dd/MM", CultureInfo.InvariantCulture), d.TotalCents))],
                ChartValueFormat.Money));
    }

    /// <summary>"+12.50 %", "-5.00 %" o "No calculable" si el período anterior no tuvo ventas.</summary>
    internal static string FormatVariation(long? basisPoints) =>
        basisPoints is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"{(value > 0 ? "+" : string.Empty)}{value / 100m:0.00} %")
            : Strings.Reports_NotCalculable;

    private string Header(SalesReportSort column, string label) =>
        SortColumn == column ? $"{label} {(SortDescending ? "▼" : "▲")}" : label;

    private void RestartFromFirstPage()
    {
        if (_suppress)
        {
            return;
        }

        CurrentPage = 1;
        RestartLoad();
    }

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return ReloadAsync();
    }

    /// <summary>Cajeros para el filtro; se conserva la elección al volver a la pantalla.</summary>
    private async Task LoadCashiersAsync()
    {
        var (completed, result) = await Runner.RunQuietlyResultAsync(
            "ListarCajerosReporte",
            () => _useCases.RunAsync<ListCashiersHandler, Result<IReadOnlyList<UserOption>>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedCashier.UserId;
        _suppress = true;
        try
        {
            CashierOptions.Clear();
            CashierOptions.Add(new CashierOption(null, Strings.Reports_CashierAll));
            foreach (var cashier in result.Value)
            {
                CashierOptions.Add(new CashierOption(cashier.Id, cashier.FullName));
            }

            SelectedCashier = CashierOptions.FirstOrDefault(c => c.UserId == selected) ?? CashierOptions[0];
        }
        finally
        {
            _suppress = false;
        }
    }
}
