using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetShiftDetail;
using Pos.Application.Printing.PrintTicket;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;

namespace Pos.Desktop.CashShifts;

/// <summary>Venta del turno en la pestaña Ventas.</summary>
public sealed record ShiftSaleRowView(ShiftSaleRowDto Sale)
{
    public string Folio => Sale.Folio;

    public string TimeText => Sale.CreatedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public string TotalText => MoneyConverter.Format(Sale.TotalCents);

    public string MethodsText => string.Join(", ", Sale.Methods.Select(PaymentMethodLabels.Of));

    public string StatusText => Sale.Status == SaleStatus.Cancelled ? Strings.Sales_StatusCancelled : Strings.Sales_StatusCompleted;
}

/// <summary>Movimiento de efectivo en la pestaña Movimientos.</summary>
public sealed record CashMovementRow(CashMovementDto Movement)
{
    public Guid Id => Movement.Id;

    public string Folio => Movement.Folio;

    public string TimeText => Movement.CreatedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public string TypeText => Movement.Type == CashMovementType.In ? Strings.Shifts_TypeIn : Strings.Shifts_TypeOut;

    public string AmountText => MoneyConverter.Format(Movement.AmountCents);

    public string Reason => Movement.Reason;

    public string UserName => Movement.CreatedByName;

    public string AuthorizedText => Movement.AuthorizedByName ?? string.Empty;
}

/// <summary>Renglón "etiqueta: valor" de la pestaña Arqueo.</summary>
public sealed record ReconciliationRow(string Label, string Value, bool IsEmphasis = false);

/// <summary>
/// Detalle de un turno (FR-021): ventas, movimientos y arqueo, con la reimpresión del corte si está
/// cerrado y el cierre del turno si sigue abierto. Es de solo lectura: nunca tiene cambios sin guardar.
/// </summary>
public sealed partial class ShiftDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly TicketPrintingService _printing;
    private readonly CashShiftDialogs _dialogs;

    private Guid _shiftId;

    public ShiftDetailViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService printing,
        CashShiftDialogs shiftDialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _printing = printing;
        _dialogs = shiftDialogs;
    }

    /// <summary>El turno cambió (se cerró desde el detalle); el listado vuelve a buscar.</summary>
    public event EventHandler? Changed;

    public override string Title => Detail is null
        ? Strings.Nav_Shifts
        : string.Format(Display, Strings.Shifts_DetailTitle, Detail.Folio);

    public ObservableCollection<ShiftSaleRowView> Sales { get; } = [];

    public ObservableCollection<CashMovementRow> Movements { get; } = [];

    public ObservableCollection<ReconciliationRow> Reconciliation { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HeaderText), nameof(ClosedText), nameof(IsClosed), nameof(IsOpen), nameof(HasClosedText))]
    public partial ShiftDetailDto? Detail { get; private set; }

    public string HeaderText => Detail is null
        ? string.Empty
        : string.Format(
            Display,
            Strings.Shifts_DetailHeader,
            Detail.Folio,
            Detail.OpenedByName,
            Detail.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            MoneyConverter.Format(Detail.OpeningFloatCents));

    public string ClosedText => Detail is { ClosedAtUtc: { } closed, ClosedByName: { } by }
        ? string.Format(Display, Strings.Shifts_ClosedBy, by, closed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
        : string.Empty;

    public bool HasClosedText => ClosedText.Length > 0;

    public bool IsClosed => Detail?.Status == CashShiftStatus.Closed;

    public bool IsOpen => Detail?.Status == CashShiftStatus.Open;

    /// <summary>Carga el turno; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid shiftId)
    {
        _shiftId = shiftId;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarTurno",
            () => _useCases.RunAsync<GetShiftDetailHandler, Result<ShiftDetailDto>>(
                h => h.HandleAsync(new GetShiftDetailQuery(shiftId), CancellationToken.None)),
            new Dictionary<string, object?> { ["ShiftId"] = shiftId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
            return false;
        }

        Apply(result.Value);
        return true;
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    /// <summary>Reimprime el corte de un turno cerrado; sale con la leyenda REIMPRESIÓN.</summary>
    [RelayCommand]
    private async Task ReprintReportAsync()
    {
        if (Detail is { Status: CashShiftStatus.Closed } detail)
        {
            await _printing.PrintAsync(PrintSource.ShiftReport(detail.Id), isReprint: true, detail.Folio, automatic: false);
        }
    }

    /// <summary>Cierra el turno abierto (incluso el de otro usuario) con el mismo diálogo de cierre.</summary>
    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (Detail is not { Status: CashShiftStatus.Open } detail)
        {
            return;
        }

        if (await _dialogs.CloseShiftAsync(detail.Id, detail.OpenedByName))
        {
            await LoadAsync(_shiftId);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private async Task PrintMovementAsync(CashMovementRow? row)
    {
        if (row is not null)
        {
            await _printing.PrintAsync(PrintSource.CashMovement(row.Id), isReprint: true, row.Folio, automatic: false);
        }
    }

    private void Apply(ShiftDetailDto detail)
    {
        Sales.Clear();
        foreach (var sale in detail.Sales)
        {
            Sales.Add(new ShiftSaleRowView(sale));
        }

        Movements.Clear();
        foreach (var movement in detail.Movements)
        {
            Movements.Add(new CashMovementRow(movement));
        }

        Reconciliation.Clear();
        var r = detail.Reconciliation;
        if (r.IsSnapshot)
        {
            Reconciliation.Add(new ReconciliationRow(Strings.Shift_Expected, MoneyConverter.Format(r.ExpectedCashCents)));
            Reconciliation.Add(new ReconciliationRow(Strings.Shift_Counted, MoneyConverter.Format(r.CountedCashCents ?? 0)));
            var difference = r.DifferenceCents ?? 0;
            Reconciliation.Add(new ReconciliationRow(
                difference switch
                {
                    > 0 => Strings.Shift_Over,
                    < 0 => Strings.Shift_Short,
                    _ => Strings.Shift_Balanced,
                },
                MoneyConverter.Format(Math.Abs(difference)),
                IsEmphasis: true));
            if (!string.IsNullOrWhiteSpace(r.Comment))
            {
                Reconciliation.Add(new ReconciliationRow(Strings.Shift_Comment, r.Comment));
            }
        }
        else
        {
            Reconciliation.Add(new ReconciliationRow(Strings.Shifts_ExpectedNow, MoneyConverter.Format(r.ExpectedCashCents), IsEmphasis: true));
            Reconciliation.Add(new ReconciliationRow(string.Empty, Strings.Shifts_NoCount));
        }

        Reconciliation.Add(new ReconciliationRow(Strings.Shifts_Float, MoneyConverter.Format(detail.OpeningFloatCents)));
        Reconciliation.Add(new ReconciliationRow(
            string.Format(Display, Strings.Shifts_SalesCount, r.SalesCount, r.CancelledCount),
            MoneyConverter.Format(r.TotalSoldCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shifts_CashSales, MoneyConverter.Format(r.CashSalesCents - r.CashCancelledCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shift_Card, MoneyConverter.Format(r.CardCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shift_Transfer, MoneyConverter.Format(r.TransferCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shifts_CashCancelled, MoneyConverter.Format(r.CashCancelledCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shifts_Deposits, MoneyConverter.Format(r.DepositsCents)));
        Reconciliation.Add(new ReconciliationRow(Strings.Shifts_Withdrawals, MoneyConverter.Format(r.WithdrawalsCents)));

        Detail = detail;
    }
}
