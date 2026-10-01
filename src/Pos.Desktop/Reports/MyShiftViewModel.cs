using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Reports.ListMyShifts;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.CashShifts;

namespace Pos.Desktop.Reports;

/// <summary>Movimiento de efectivo con los textos ya formateados.</summary>
public sealed record MyShiftMovementItem(MyShiftMovement Movement)
{
    public string DateText => Movement.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TypeText => Movement.Type == CashMovementType.In ? Strings.Reports_MyShift_MovementIn : Strings.Reports_MyShift_MovementOut;

    public string AmountText => MoneyConverter.Format(Movement.AmountCents);

    public string Reason => Movement.Reason;
}

/// <summary>Turno reciente en la lista de "Mi turno".</summary>
public sealed record MyShiftListEntry(MyShiftListItem Item)
{
    public string Folio => Item.FolioText;

    public string DateText => Item.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string StatusText => Item.IsOpen ? Strings.Reports_MyShift_Open : Strings.Reports_MyShift_Closed;

    public string TotalText => MoneyConverter.Format(Item.TotalSoldCents);
}

/// <summary>
/// "Mi turno": el Cajero ve y exporta a PDF el resumen de su propio turno (Historia 6). Con el turno abierto
/// no ve efectivo esperado ni contado; sin turno abierto elige entre sus últimos turnos.
/// </summary>
public sealed partial class MyShiftViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly ReportExportCoordinator _exporter;
    private Guid? _loadedShiftId;
    private bool _suppress;

    public MyShiftViewModel(UseCases useCases, OperationRunner runner, ReportExportCoordinator exporter)
    {
        _useCases = useCases;
        _runner = runner;
        _exporter = exporter;
    }

    public override string Title => Strings.Reports_MyShift_Title;

    public ObservableCollection<MyShiftListEntry> Recent { get; } = [];

    public ObservableCollection<MyShiftMovementItem> Movements { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    public partial bool HasSummary { get; private set; }

    [ObservableProperty]
    public partial bool ShowList { get; private set; }

    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial MyShiftListEntry? SelectedShift { get; set; }

    [ObservableProperty]
    public partial string FolioText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PeriodText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string FloatText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SalesText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SalesTotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DepositsText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string WithdrawalsText { get; private set; } = string.Empty;

    /// <summary>Bloque "Crédito" del turno (014, FR-012); sin montos de efectivo esperado.</summary>
    [ObservableProperty]
    public partial bool HasCredit { get; private set; }

    [ObservableProperty]
    public partial string OnAccountSalesText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PaymentsCashText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PaymentsNonCashText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PaymentVoidsText { get; private set; } = string.Empty;

    /// <summary>Solo con el turno cerrado se muestran efectivo esperado, contado y diferencia.</summary>
    [ObservableProperty]
    public partial bool ShowCashCount { get; private set; }

    [ObservableProperty]
    public partial string ExpectedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CountedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DifferenceText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOpenShift { get; private set; }

    public override async Task OnActivatedAsync()
    {
        IsBusy = true;
        try
        {
            await _runner.RunAsync("MiTurno", LoadAsync);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedShiftChanged(MyShiftListEntry? value)
    {
        if (!_suppress && value is not null)
        {
            _ = LoadSelectedAsync(value.Item.ShiftId);
        }
    }

    private bool CanExport() => HasSummary;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync() => await _exporter.ExportAsync(ExportRequest.ForMyShift(_loadedShiftId), FolioText);

    private async Task LoadSelectedAsync(Guid shiftId)
    {
        IsBusy = true;
        try
        {
            await _runner.RunAsync("MiTurnoDetalle", () => ShowSummaryAsync(shiftId));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        // Primero el turno abierto propio; si no hay, la lista de los más recientes.
        var found = await ShowSummaryAsync(null);
        if (found)
        {
            ShowList = false;
            Message = string.Empty;
            return;
        }

        HasSummary = false;
        var result = await _useCases.RunAsync<ListMyShiftsHandler, Result<IReadOnlyList<MyShiftListItem>>>(h => h.HandleAsync(CancellationToken.None));
        _suppress = true;
        try
        {
            Recent.Clear();
            if (result.IsSuccess)
            {
                foreach (var item in result.Value)
                {
                    Recent.Add(new MyShiftListEntry(item));
                }
            }

            SelectedShift = null;
        }
        finally
        {
            _suppress = false;
        }

        ShowList = Recent.Count > 0;
        Message = Recent.Count > 0 ? Strings.Reports_MyShift_NoOpen : Strings.Reports_MyShift_NoShifts;
    }

    private async Task<bool> ShowSummaryAsync(Guid? shiftId)
    {
        var result = await _useCases.RunAsync<GetMyShiftSummaryHandler, Result<MyShiftSummary>>(h => h.HandleAsync(shiftId, CancellationToken.None));
        if (!result.IsSuccess)
        {
            return false;
        }

        Apply(result.Value);
        return true;
    }

    private void Apply(MyShiftSummary summary)
    {
        _loadedShiftId = summary.ShiftId;
        FolioText = summary.FolioText;
        IsOpenShift = summary.IsOpen;
        PeriodText = summary.ClosedAtUtc is { } closed
            ? string.Format(CultureInfo.CurrentCulture, Strings.Reports_MyShift_ClosedAt, Local(summary.OpenedAtUtc), Local(closed))
            : string.Format(CultureInfo.CurrentCulture, Strings.Reports_MyShift_OpenedSince, Local(summary.OpenedAtUtc));
        FloatText = MoneyConverter.Format(summary.OpeningFloatCents);
        SalesText = summary.SalesCount.ToString("N0", CultureInfo.CurrentCulture);
        SalesTotalText = MoneyConverter.Format(summary.TotalSoldCents);
        DepositsText = MoneyConverter.Format(summary.DepositsCents);
        WithdrawalsText = MoneyConverter.Format(summary.WithdrawalsCents);
        HasCredit = summary.Credit is not null;
        OnAccountSalesText = MoneyConverter.Format(summary.Credit?.OnAccountSalesCents ?? 0);
        PaymentsCashText = MoneyConverter.Format(summary.Credit?.PaymentsCashCents ?? 0);
        PaymentsNonCashText = MoneyConverter.Format(summary.Credit?.PaymentsNonCashCents ?? 0);
        PaymentVoidsText = MoneyConverter.Format(summary.Credit?.PaymentVoidsCents ?? 0);

        ShowCashCount = !summary.IsOpen && summary.ExpectedCashCents is not null;
        ExpectedText = summary.ExpectedCashCents is { } expected ? MoneyConverter.Format(expected) : string.Empty;
        CountedText = summary.CountedCashCents is { } counted ? MoneyConverter.Format(counted) : string.Empty;
        DifferenceText = summary.DifferenceCents is { } difference ? CashCountRowItem.Signed(difference) : string.Empty;

        Movements.Clear();
        foreach (var movement in summary.Movements)
        {
            Movements.Add(new MyShiftMovementItem(movement));
        }

        HasSummary = true;
    }

    private static string Local(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
