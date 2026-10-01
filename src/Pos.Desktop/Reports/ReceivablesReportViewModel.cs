using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Receivables;
using Pos.Application.Receivables.SaveReceivablesSettings;
using Pos.Application.Reports.GetReceivablesReport;
using Pos.Desktop.Common;
using Pos.Desktop.Customers;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Reports;

/// <summary>Opción del filtro de estado; el valor nulo significa "Todos".</summary>
public sealed record ReceivablesStatusOption(ReceivablesStatusFilter? Status, string Label);

/// <summary>Fila del reporte "Créditos" con los textos ya formateados.</summary>
public sealed record ReceivablesReportItem(ReceivablesReportRow Row)
{
    public Guid CustomerId => Row.CustomerId;

    public string Name => Row.Name;

    public string BalanceText => MoneyConverter.Format(Row.BalanceCents);

    public string LimitText => MoneyConverter.Format(Row.LimitCents);

    public string LastPaymentText => Row.LastPaymentAtUtc is { } last
        ? last.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        : Strings.Receivables_NoPayment;

    public string DaysOverdueText => Row.DaysOverdue > 0 ? Row.DaysOverdue.ToString(MoneyConverter.Culture) : string.Empty;

    public bool IsOverdue => Row.IsOverdue;

    /// <summary>Etiquetas de estado: vencido o al día, y al límite (independiente).</summary>
    public string TagsText => string.Join(
        " · ",
        new[] { Row.IsOverdue ? Strings.Receivables_StatusOverdue : Strings.Receivables_StatusCurrent, Row.IsAtLimit ? Strings.Receivables_StatusAtLimit : null }
            .Where(t => t is not null));
}

/// <summary>
/// "Reportes > Créditos" (014, Historia 4): una fila por cliente con saldo, filtros por estado y texto, y
/// totales sobre las filas visibles. Todo lo calcula <c>GetReceivablesReport</c>; se consulta al abrir y
/// al pulsar "Actualizar", sin caché. El Administrador edita aquí el plazo de pago.
/// </summary>
public sealed partial class ReceivablesReportViewModel : PageViewModel
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Navigator? _navigator;
    private readonly ICurrentPermissions? _permissions;
    private int _loadVersion;

    public ReceivablesReportViewModel(UseCases useCases, OperationRunner runner, Navigator? navigator = null, ICurrentPermissions? permissions = null)
    {
        _useCases = useCases;
        _runner = runner;
        _navigator = navigator;
        _permissions = permissions;
        StatusOptions =
        [
            new(null, Strings.Receivables_StatusAll),
            new(ReceivablesStatusFilter.Current, Strings.Receivables_StatusCurrent),
            new(ReceivablesStatusFilter.Overdue, Strings.Receivables_StatusOverdue),
            new(ReceivablesStatusFilter.AtLimit, Strings.Receivables_StatusAtLimit),
        ];
        SelectedStatus = StatusOptions[0];
    }

    public override string Title => Strings.Receivables_Title;

    public IReadOnlyList<ReceivablesStatusOption> StatusOptions { get; }

    public ObservableCollection<ReceivablesReportItem> Rows { get; } = [];

    [ObservableProperty]
    public partial ReceivablesStatusOption SelectedStatus { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCustomerCommand))]
    public partial ReceivablesReportItem? SelectedRow { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    /// <summary>El plazo de pago lo edita solo quien tiene <c>ManageCustomerCredit</c>.</summary>
    public bool CanEditTerm => _permissions?.Has(Permission.ManageCustomerCredit) ?? false;

    [ObservableProperty]
    public partial string TermText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? TermMessage { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public override Task OnActivatedAsync() => LoadAsync();

    partial void OnSelectedStatusChanged(ReceivablesStatusOption value) => _ = LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenCustomerAsync()
    {
        if (SelectedRow is { } row && _navigator is not null)
        {
            await _navigator.NavigateAsync(CustomersModule.ListPageId, row.CustomerId);
        }
    }

    private bool HasSelection() => SelectedRow is not null;

    [RelayCommand]
    private async Task SaveTermAsync()
    {
        TermMessage = null;
        if (!int.TryParse(TermText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            TermMessage = Strings.Receivables_TermInvalid;
            return;
        }

        var command = new SaveReceivablesSettingsCommand(days);
        var (completed, result) = await _runner.RunAsync(
            "GuardarPlazoDePago",
            () => _useCases.RunAsync<SaveReceivablesSettingsHandler, Result<ReceivablesSettings>>(h => h.HandleAsync(command, CancellationToken.None)));
        if (!completed || result is null)
        {
            return;
        }

        TermMessage = result.Error switch
        {
            null => Strings.Receivables_TermSaved,
            ValidationFailed validation => validation.Errors is [{ } first, ..] ? first.Message : Strings.Receivables_TermInvalid,
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        };
        if (result.IsSuccess)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        var query = new GetReceivablesReportQuery(SelectedStatus.Status, SearchText);
        var (completed, result) = await _runner.RunAsync(
            "ReporteCreditos",
            () => _useCases.RunAsync<GetReceivablesReportHandler, Result<ReceivablesReport>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Status"] = query.Status?.ToString() });
        if (!completed || result is null || version != _loadVersion)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error is ModuleNotLicensed ? Strings.License_ModuleNotLicensed : Strings.Common_Forbidden;
            return;
        }

        ErrorMessage = null;
        var report = result.Value;
        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new ReceivablesReportItem(row));
        }

        IsEmpty = Rows.Count == 0;
        TotalText = MoneyConverter.Format(report.TotalBalanceCents);
        CountText = report.CustomerCount.ToString("N0", Display);
        if (string.IsNullOrWhiteSpace(TermText))
        {
            TermText = report.PaymentTermDays.ToString(CultureInfo.InvariantCulture);
        }
    }
}
