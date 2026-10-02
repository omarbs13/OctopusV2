using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetPurchaseReport;
using Pos.Application.Suppliers;
using Pos.Application.Suppliers.ListSuppliersForReport;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Purchases;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Reports;

/// <summary>Opción del filtro de proveedor; nulo = todos. Los inactivos se marcan "(inactivo)".</summary>
public sealed record SupplierFilterChoice(Guid? SupplierId, string Label);

/// <summary>Fila del reporte de compras con los textos ya formateados.</summary>
public sealed record PurchaseReportItem(PurchaseReportRow Row)
{
    public Guid PurchaseId => Row.PurchaseId;

    public string DateText => Row.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public string SupplierName => Row.SupplierName;

    public string InvoiceNumber => Row.InvoiceNumber;

    public string LinesText => Row.LineCount.ToString(MoneyConverter.Culture);

    public string SubtotalText => MoneyConverter.Format(Row.SubtotalCents);

    public string TaxText => MoneyConverter.Format(Row.TaxCents);

    public string TotalText => MoneyConverter.Format(Row.TotalCents);

    public string UserName => Row.RegisteredByName;

    public bool IsVoided => Row.IsVoided;

    /// <summary>Las anuladas se muestran en gris.</summary>
    public double RowOpacity => Row.IsVoided ? 0.55 : 1;
}

/// <summary>
/// "Reportes > Compras" (020, Historia 3): filtros por proveedor (incluidos inactivos), fechas de factura y total;
/// 100 compras por página y acumulados de todo el filtro. Todo lo calcula <c>GetPurchaseReport</c>; un filtro
/// inválido se marca en su campo y no muestra resultados (FR-023).
/// </summary>
public sealed partial class PurchaseReportViewModel : PageViewModel
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<PurchaseDetailViewModel> _detailFactory;
    private int _loadVersion;

    public PurchaseReportViewModel(UseCases useCases, OperationRunner runner, Func<PurchaseDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _detailFactory = detailFactory;
        SupplierOptions.Add(new SupplierFilterChoice(null, Strings.PurchaseReport_AllSuppliers));
        SelectedSupplier = SupplierOptions[0];
        ResetSummary();
    }

    public override string Title => Strings.PurchaseReport_Title;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<SupplierFilterChoice> SupplierOptions { get; } = [];

    public ObservableCollection<PurchaseReportItem> Rows { get; } = [];

    [ObservableProperty]
    public partial SupplierFilterChoice SelectedSupplier { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial string MinTotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxTotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeVoided { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenPurchaseCommand))]
    public partial PurchaseReportItem? SelectedRow { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string? DateError { get; private set; }

    [ObservableProperty]
    public partial string? MinTotalError { get; private set; }

    [ObservableProperty]
    public partial string? MaxTotalError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SubtotalSumText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxSumText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalSumText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary => string.Format(CultureInfo.CurrentCulture, Strings.PurchaseReport_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadSuppliersAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private Task ApplyAsync()
    {
        CurrentPage = 1;
        return LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    /// <summary>Doble clic o Enter: abre el detalle de la compra.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenPurchaseAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var detail = _detailFactory();
        detail.Voided += (_, _) => _ = LoadAsync();
        if (await detail.LoadAsync(row.PurchaseId))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return LoadAsync();
    }

    private async Task LoadSuppliersAsync()
    {
        var (completed, result) = await _runner.RunAsync(
            "ProveedoresReporteCompras",
            () => _useCases.RunAsync<ListSuppliersForReportHandler, Result<IReadOnlyList<SupplierFilterOption>>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedSupplier.SupplierId;
        SupplierOptions.Clear();
        SupplierOptions.Add(new SupplierFilterChoice(null, Strings.PurchaseReport_AllSuppliers));
        foreach (var supplier in result.Value)
        {
            SupplierOptions.Add(new SupplierFilterChoice(
                supplier.Id,
                supplier.IsActive ? supplier.Name : $"{supplier.Name} {Strings.PurchaseReport_Inactive}"));
        }

        SelectedSupplier = SupplierOptions.FirstOrDefault(o => o.SupplierId == selected) ?? SupplierOptions[0];
    }

    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        var query = new GetPurchaseReportQuery(
            SelectedSupplier.SupplierId,
            FromDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDate is { } to ? DateOnly.FromDateTime(to) : null,
            MinTotalText,
            MaxTotalText,
            IncludeVoided,
            CurrentPage);
        var (completed, result) = await _runner.RunAsync(
            "ReporteCompras",
            () => _useCases.RunAsync<GetPurchaseReportHandler, Result<PurchaseReportPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["SupplierId"] = query.SupplierId, ["Page"] = query.Page });
        if (!completed || result is null || version != _loadVersion)
        {
            return;
        }

        DateError = MinTotalError = MaxTotalError = ErrorMessage = null;
        if (!result.IsSuccess)
        {
            // Filtro inválido: mensaje en el campo y sin resultados (FR-023).
            Rows.Clear();
            IsEmpty = false;
            ResetSummary();
            switch (result.Error)
            {
                case ValidationFailed validation:
                    foreach (var error in validation.Errors)
                    {
                        SetError(error);
                    }

                    break;
                case ModuleNotLicensed:
                    ErrorMessage = Strings.License_ModuleNotLicensed;
                    break;
                default:
                    ErrorMessage = Strings.Common_Forbidden;
                    break;
            }

            return;
        }

        var page = result.Value;
        Rows.Clear();
        foreach (var row in page.Rows)
        {
            Rows.Add(new PurchaseReportItem(row));
        }

        IsEmpty = Rows.Count == 0;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        CountText = page.PurchaseCount.ToString("N0", Display);
        SubtotalSumText = MoneyConverter.Format(page.SubtotalSumCents);
        TaxSumText = MoneyConverter.Format(page.TaxSumCents);
        TotalSumText = MoneyConverter.Format(page.TotalSumCents);
    }

    private void SetError(FieldError error)
    {
        switch (error.Field)
        {
            case ReportFields.FromDate:
            case ReportFields.ToDate:
                DateError = error.Message;
                break;
            case ReportFields.MinTotal:
                MinTotalError = error.Message;
                break;
            case ReportFields.MaxTotal:
                MaxTotalError = error.Message;
                break;
            default:
                ErrorMessage = error.Message;
                break;
        }
    }

    private void ResetSummary()
    {
        TotalCount = 0;
        TotalPages = 1;
        CountText = 0.ToString("N0", Display);
        SubtotalSumText = TaxSumText = TotalSumText = MoneyConverter.Format(0);
    }
}
