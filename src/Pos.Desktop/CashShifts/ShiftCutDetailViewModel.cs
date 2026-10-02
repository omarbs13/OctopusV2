using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetShiftCut;
using Pos.Application.Printing.PrintTicket;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.CashShifts;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// Vista de un Corte X o Z (017, contracts/ui.md "Vista del corte"): las mismas cifras fijas que el
/// ticket. Recién generado ofrece "Imprimir"; desde el histórico, "Reimprimir". Es de solo lectura.
/// </summary>
public sealed partial class ShiftCutDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly TicketPrintingService _printing;

    public ShiftCutDetailViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, TicketPrintingService printing)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _printing = printing;
    }

    public override string Title => Cut is null
        ? Strings.Nav_CashCuts
        : string.Format(Display, Cut.Type == ShiftCutType.Readout ? Strings.Cut_TitleReadout : Strings.Cut_TitleClosing, Cut.Folio);

    public ObservableCollection<ReconciliationRow> Header { get; } = [];

    public ObservableCollection<ReconciliationRow> Figures { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(IsReadout))]
    public partial ShiftCutReportDto? Cut { get; private set; }

    /// <summary>Verdadero cuando se abrió desde el histórico: el ticket sale con la leyenda REIMPRESIÓN.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrintText))]
    public partial bool IsReprint { get; private set; }

    public bool IsReadout => Cut?.Type == ShiftCutType.Readout;

    public string PrintText => IsReprint ? Strings.Cut_Reprint : Strings.Cut_Print;

    /// <summary>Carga el corte; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid cutId, bool isReprint)
    {
        IsReprint = isReprint;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarCorte",
            () => _useCases.RunAsync<GetShiftCutHandler, Result<ShiftCutReportDto>>(
                h => h.HandleAsync(new GetShiftCutQuery(cutId), CancellationToken.None)),
            new Dictionary<string, object?> { ["CutId"] = cutId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(
                Strings.Common_InfoTitle,
                result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Cut_NotFound);
            return false;
        }

        Apply(result.Value);
        return true;
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    /// <summary>Imprime el corte; una falla de impresión no afecta al corte, que ya está guardado.</summary>
    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Cut is { } cut)
        {
            await _printing.PrintAsync(PrintSource.ShiftCut(cut.CutId), IsReprint, cut.Folio, automatic: false);
        }
    }

    private void Apply(ShiftCutReportDto cut)
    {
        static string Local(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        Header.Clear();
        Header.Add(new ReconciliationRow(Strings.Cut_Shift, cut.ShiftFolio));
        Header.Add(new ReconciliationRow(Strings.Cut_Register, cut.RegisterName));
        Header.Add(new ReconciliationRow(Strings.Cut_Owner, cut.ShiftOwnerName));
        Header.Add(new ReconciliationRow(Strings.Cut_GeneratedBy, cut.GeneratedByName));
        if (cut.AuthorizedByName is { } authorizer)
        {
            Header.Add(new ReconciliationRow(string.Format(Display, Strings.Cut_AuthorizedBy, authorizer), string.Empty));
        }

        Header.Add(new ReconciliationRow(Strings.Cut_OpenedAt, Local(cut.ShiftOpenedAtUtc)));
        Header.Add(new ReconciliationRow(Strings.Cut_GeneratedAt, Local(cut.GeneratedAtUtc)));

        Figures.Clear();
        Figures.Add(new ReconciliationRow(Strings.Shifts_Float, MoneyConverter.Format(cut.OpeningFloatCents)));
        Figures.Add(new ReconciliationRow(string.Format(Display, Strings.Shifts_SalesCount, cut.SalesCount, cut.CancelledCount), string.Empty));
        Figures.Add(new ReconciliationRow(Strings.Cut_TotalSold, MoneyConverter.Format(cut.TotalSoldCents), IsEmphasis: true));
        Figures.Add(new ReconciliationRow(Strings.Shifts_CashSales, MoneyConverter.Format(cut.CashSalesCents - cut.CashCancelledCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_Card, MoneyConverter.Format(cut.CardCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_Transfer, MoneyConverter.Format(cut.TransferCents)));
        Figures.Add(new ReconciliationRow(Strings.Shifts_CashCancelled, MoneyConverter.Format(cut.CashCancelledCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_CashRefunds, MoneyConverter.Format(cut.CashRefundsCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_NonCashRefunds, MoneyConverter.Format(cut.NonCashRefundsCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_CreditNotesIssued, MoneyConverter.Format(cut.CreditNotesIssuedCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_CreditSection, string.Empty, IsEmphasis: true));
        Figures.Add(new ReconciliationRow(Strings.Shift_OnAccountSales, MoneyConverter.Format(cut.Credit.OnAccountSalesCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_PaymentsCash, MoneyConverter.Format(cut.Credit.PaymentsCashCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_PaymentsNonCash, MoneyConverter.Format(cut.Credit.PaymentsNonCashCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_PaymentVoids, MoneyConverter.Format(cut.Credit.PaymentVoidsCents)));
        Figures.Add(new ReconciliationRow(Strings.Shifts_Deposits, MoneyConverter.Format(cut.DepositsCents)));
        Figures.Add(new ReconciliationRow(Strings.Shifts_Withdrawals, MoneyConverter.Format(cut.WithdrawalsCents)));
        Figures.Add(new ReconciliationRow(Strings.Shift_Expected, MoneyConverter.Format(cut.ExpectedCashCents), IsEmphasis: true));

        // Solo el Corte Z tiene arqueo (FR-004a).
        if (cut.Type == ShiftCutType.Closing)
        {
            Figures.Add(new ReconciliationRow(Strings.Shift_Counted, MoneyConverter.Format(cut.CountedCashCents ?? 0)));
            var difference = cut.DifferenceCents ?? 0;
            Figures.Add(new ReconciliationRow(
                difference switch
                {
                    > 0 => Strings.Shift_Over,
                    < 0 => Strings.Shift_Short,
                    _ => Strings.Shift_Balanced,
                },
                MoneyConverter.Format(Math.Abs(difference)),
                IsEmphasis: true));
            if (!string.IsNullOrWhiteSpace(cut.Comment))
            {
                Figures.Add(new ReconciliationRow(Strings.Shift_Comment, cut.Comment));
            }
        }

        Cut = cut;
    }
}
