using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.SaveReportSettings;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Desktop.Reports;

/// <summary>Fila del arqueo con los textos ya formateados; un turno abierto se muestra "En curso" sin cifras de efectivo.</summary>
public sealed record CashCountRowItem(CashCountRow Row)
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public string Shift => Row.FolioText;

    public string Cashier => Row.CashierName;

    public string OpenedText => Row.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    public string ClosedText => Row.ClosedAtUtc is { } closed
        ? closed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture)
        : Strings.Reports_CashCount_InProgress;

    public string FloatText => MoneyConverter.Format(Row.OpeningFloatCents);

    public string SoldText => MoneyConverter.Format(Row.TotalSoldCents);

    public string DepositsText => MoneyConverter.Format(Row.DepositsCents);

    public string WithdrawalsText => MoneyConverter.Format(Row.WithdrawalsCents);

    public string ExpectedText => Row.ExpectedCashCents is { } v ? MoneyConverter.Format(v) : string.Empty;

    public string CountedText => Row.CountedCashCents is { } v ? MoneyConverter.Format(v) : string.Empty;

    public string DifferenceText => Row.DifferenceCents is { } v ? Signed(v) : string.Empty;

    public string PercentText => Row.IsOpen
        ? string.Empty
        : Row.DifferenceBasisPoints is { } bp
            ? string.Create(Culture, $"{(bp > 0 ? "+" : string.Empty)}{bp / 100m:0.##} %")
            : Strings.Reports_NotCalculable;

    public bool IsAlert => Row.IsAlert;

    internal static string Signed(long cents) => (cents > 0 ? "+" : string.Empty) + MoneyConverter.Format(cents);
}

/// <summary>Reportes > Arqueo: turnos del período con diferencia, alertas y umbral configurable (Historia 2).</summary>
public sealed partial class CashCountReportViewModel : ReportPageViewModel
{
    private readonly UseCases _useCases;
    private bool _suppress;

    public CashCountReportViewModel(
        UseCases useCases,
        OperationRunner runner,
        IChartRenderer renderer,
        ReportExportCoordinator exporter,
        ICurrentPermissions? permissions = null)
        : base(runner, renderer, exporter, ReportPreset.ThisMonth)
    {
        _useCases = useCases;
        CanEditThreshold = permissions?.Has(Permission.ManageSettings) ?? false;
        CashierOptions = [new CashierOption(null, Strings.Reports_CashierAll)];
        _suppress = true;
        SelectedCashier = CashierOptions[0];
        _suppress = false;
        ThresholdText = FormatPercent(CashDifferenceRule.DefaultThresholdBasisPoints);
    }

    public override string Title => Strings.Nav_ReportCashCount;

    protected override string OperationName => "ReporteArqueo";

    /// <summary>Solo quien tiene <c>ManageSettings</c> cambia el umbral; los demás lo ven de solo lectura.</summary>
    public bool CanEditThreshold { get; }

    public ObservableCollection<CashierOption> CashierOptions { get; }

    public ObservableCollection<CashCountRowItem> Rows { get; } = [];

    [ObservableProperty]
    public partial CashierOption SelectedCashier { get; set; }

    [ObservableProperty]
    public partial string ThresholdText { get; set; }

    [ObservableProperty]
    public partial string? ThresholdMessage { get; private set; }

    [ObservableProperty]
    public partial string ClosedShiftsText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalSoldText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AccumulatedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AlertText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasAlerts { get; private set; }

    public override async Task OnActivatedAsync()
    {
        await LoadCashiersAsync();
        await ReloadAsync();
    }

    partial void OnSelectedCashierChanged(CashierOption value)
    {
        if (!_suppress)
        {
            RestartLoad();
        }
    }

    [RelayCommand]
    private async Task SaveThresholdAsync()
    {
        if (!TryParsePercent(ThresholdText, out var basisPoints))
        {
            ThresholdMessage = Strings.Reports_CashCount_ThresholdInvalid;
            return;
        }

        var (completed, result) = await Runner.RunAsync(
            "GuardarUmbralArqueo",
            () => _useCases.RunAsync<SaveReportSettingsHandler, Result>(h => h.HandleAsync(basisPoints, CancellationToken.None)));
        if (!completed || result is null)
        {
            return;
        }

        ThresholdMessage = result.IsSuccess ? Strings.Reports_CashCount_ThresholdSaved : Strings.Reports_CashCount_ThresholdInvalid;
        if (result.IsSuccess)
        {
            await ReloadAsync();
        }
    }

    protected override ExportRequest CreateExportRequest(ExportFormat format) =>
        ExportRequest.ForCashCount(new CashCountReportQuery(Picker.Period, SelectedCashier.UserId), format);

    protected override async Task LoadAsync(Func<bool> isCurrent)
    {
        var query = new CashCountReportQuery(Picker.Period, SelectedCashier.UserId);
        var result = await _useCases.RunAsync<GetCashCountReportHandler, Result<CashCountReport>>(
            h => h.HandleAsync(query, CancellationToken.None));
        if (!isCurrent() || !result.IsSuccess)
        {
            return;
        }

        Apply(result.Value);
    }

    private void Apply(CashCountReport report)
    {
        IsEmpty = report.Rows.Count == 0;
        ThresholdText = FormatPercent(report.ThresholdBasisPoints);
        ClosedShiftsText = report.Totals.ClosedShifts.ToString("N0", CultureInfo.CurrentCulture);
        TotalSoldText = MoneyConverter.Format(report.Totals.TotalSoldCents);
        AccumulatedText = CashCountRowItem.Signed(report.Totals.AccumulatedDifferenceCents);

        HasAlerts = report.AlertCount > 0;
        AlertText = string.Format(CultureInfo.CurrentCulture, Strings.Reports_CashCount_Alerts, report.AlertCount);

        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new CashCountRowItem(row));
        }

        var closed = report.Rows.Where(r => !r.IsOpen && r.DifferenceCents is not null).ToList();
        Chart.Set(closed.Count == 0
            ? null
            : new ChartSpec(
                ChartKind.Bars,
                Strings.Reports_CashCount_ChartTitle,
                [.. closed.Select(r => new ChartPoint(r.FolioText, r.DifferenceCents!.Value))],
                ChartValueFormat.Money));
    }

    /// <summary>Centésimas de por ciento a texto: 500 → "5", 25 → "0.25".</summary>
    internal static string FormatPercent(long basisPoints) =>
        (basisPoints / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Acepta "5", "5.5" o "5,5" y lo convierte a centésimas de por ciento; falla fuera de 0.01 a 100.</summary>
    internal static bool TryParsePercent(string? text, out long basisPoints)
    {
        basisPoints = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.').TrimEnd('%').Trim();
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
        {
            return false;
        }

        var value = decimal.Round(percent * 100, 0, MidpointRounding.AwayFromZero);
        if (value is < ReportSettings.MinThresholdBasisPoints or > ReportSettings.MaxThresholdBasisPoints)
        {
            return false;
        }

        basisPoints = (long)value;
        return true;
    }

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
