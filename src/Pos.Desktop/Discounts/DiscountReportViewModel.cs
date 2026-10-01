using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.GetDiscountReport;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Reports;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Discounts;
using Pos.Domain.Reports;

namespace Pos.Desktop.Discounts;

/// <summary>Fila del reporte de descuentos con los textos ya formateados.</summary>
public sealed record DiscountReportRowItem(DiscountReportRow Row)
{
    public string Folio => Row.Folio;

    public string DateText => Row.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string Cashier => Row.CashierName;

    public string KindText => DiscountMessages.KindText(Row.Kind);

    public string ValueText => Row.Mode == DiscountMode.Percent
        ? DiscountValue.Create(Row.Mode, Row.Value).ToString()
        : MoneyConverter.Format(Row.Value);

    public string AmountText => MoneyConverter.Format(Row.AmountCents);

    public string AuthorizedBy => Row.AuthorizedByName ?? string.Empty;

    public string Coupon => Row.CouponCode ?? string.Empty;
}

/// <summary>Opción del filtro de tipo; <c>Kind</c> nulo = todos.</summary>
public sealed record DiscountKindOption(DiscountKind? Kind, string Label);

/// <summary>
/// Descuentos > Reporte (015, FR-018): período con los presets de 009, filtros por cajero y tipo, total
/// descontado y cantidad, tabla paginada de 100 y exportación en los formatos de 009.
/// </summary>
public sealed partial class DiscountReportViewModel : ReportPageViewModel
{
    private readonly UseCases _useCases;
    private bool _suppress;

    public DiscountReportViewModel(UseCases useCases, OperationRunner runner, IChartRenderer renderer, ReportExportCoordinator exporter)
        : base(runner, renderer, exporter, ReportPreset.Today)
    {
        _useCases = useCases;
        CashierOptions = [new CashierOption(null, Strings.Reports_CashierAll)];
        KindOptions =
        [
            new DiscountKindOption(null, Strings.DiscountReport_AllKinds),
            .. Enum.GetValues<DiscountKind>().Select(k => new DiscountKindOption(k, DiscountMessages.KindText(k))),
        ];
        _suppress = true;
        SelectedCashier = CashierOptions[0];
        SelectedKind = KindOptions[0];
        _suppress = false;
    }

    public override string Title => Strings.DiscountReport_Title;

    protected override string OperationName => "ReporteDescuentos";

    public ObservableCollection<CashierOption> CashierOptions { get; }

    public IReadOnlyList<DiscountKindOption> KindOptions { get; }

    public ObservableCollection<DiscountReportRowItem> Rows { get; } = [];

    [ObservableProperty]
    public partial CashierOption SelectedCashier { get; set; }

    [ObservableProperty]
    public partial DiscountKindOption SelectedKind { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int CurrentPage { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalRows { get; private set; }

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.DiscountReport_PageSummary, TotalRows, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadCashiersAsync();
        await ReloadAsync();
    }

    partial void OnSelectedCashierChanged(CashierOption value) => Restart();

    partial void OnSelectedKindChanged(DiscountKindOption value) => Restart();

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync()
    {
        CurrentPage--;
        return ReloadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync()
    {
        CurrentPage++;
        return ReloadAsync();
    }

    protected override ExportRequest CreateExportRequest(ExportFormat format) =>
        ExportRequest.ForDiscounts(Query(page: 1), format);

    protected override async Task LoadAsync(Func<bool> isCurrent)
    {
        var query = Query(CurrentPage);
        var result = await _useCases.RunAsync<GetDiscountReportHandler, Result<DiscountReport>>(
            h => h.HandleAsync(query, CancellationToken.None));
        if (!isCurrent() || !result.IsSuccess)
        {
            return;
        }

        var report = result.Value;
        IsEmpty = report.Count == 0;
        TotalText = MoneyConverter.Format(report.TotalDiscountCents);
        CountText = report.Count.ToString("N0", CultureInfo.CurrentCulture);
        TotalRows = report.TotalRows;
        TotalPages = report.TotalPages;
        CurrentPage = report.Page;
        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new DiscountReportRowItem(row));
        }
    }

    private DiscountReportQuery Query(int page) =>
        new(Picker.Period, SelectedCashier.UserId, SelectedKind.Kind, page, ReportPaging.ScreenPageSize);

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private void Restart()
    {
        if (_suppress)
        {
            return;
        }

        CurrentPage = 1;
        RestartLoad();
    }

    private async Task LoadCashiersAsync()
    {
        var (completed, result) = await Runner.RunQuietlyResultAsync(
            "ListarCajerosReporteDescuentos",
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
