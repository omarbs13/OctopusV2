using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.CloseShift;
using Pos.Application.CashShifts.CountShiftCash;
using Pos.Application.Printing.PrintTicket;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;

namespace Pos.Desktop.CashShifts;

public enum CloseShiftStep
{
    Count,
    Figures,
    Done,
}

/// <summary>
/// Corte Z (cierre de turno, 008 y 017) en tres pasos (Historia 3, contracts/ui.md): 1 conteo sin
/// ninguna cifra esperada (arqueo ciego, FR-014); 2 cifras con comentario obligatorio si hay
/// diferencia; 3 cierre e impresión del Corte Z. Solo presenta: esperado y diferencia los calcula <c>CountShiftCash</c> (Principio III).
/// </summary>
public sealed partial class CloseShiftViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly TicketPrintingService _printing;
    private readonly Guid _shiftId;
    private readonly Action<bool> _finished;

    private ShiftCountResult? _count;
    private bool _discardHeldSale;
    private ClosedShift? _closed;

    public CloseShiftViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService printing,
        Guid shiftId,
        string? ownerName,
        Action<bool> finished)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _printing = printing;
        _shiftId = shiftId;
        OwnerNote = ownerName is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.Shift_CloseOwnerNote, ownerName);
        _finished = finished;
    }

    /// <summary>"Turno de {nombre}" cuando un administrador cierra el turno de otro usuario.</summary>
    public string? OwnerNote { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCountStep), nameof(IsFiguresStep), nameof(IsDoneStep))]
    public partial CloseShiftStep Step { get; private set; } = CloseShiftStep.Count;

    public bool IsCountStep => Step == CloseShiftStep.Count;

    public bool IsFiguresStep => Step == CloseShiftStep.Figures;

    public bool IsDoneStep => Step == CloseShiftStep.Done;

    [ObservableProperty]
    public partial string CountedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CountedError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCloseCommand))]
    public partial string Comment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CommentError { get; private set; }

    [ObservableProperty]
    public partial string? Notice { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CountCommand), nameof(ConfirmCloseCommand), nameof(PrintReportCommand), nameof(FinishCommand))]
    public partial bool IsBusy { get; private set; }

    // Cifras del paso 2 (ya formateadas; las calcula el caso de uso).
    [ObservableProperty]
    public partial string ExpectedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CountedFigureText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DifferenceText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DifferenceLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CardText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TransferText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommentRequiredText))]
    public partial bool NeedsComment { get; private set; }

    public string? CommentRequiredText => NeedsComment ? Strings.Shift_CommentRequired : null;

    /// <summary>"Corte Z Z-000001 · Turno T-000123 cerrado" (017).</summary>
    public string DoneText => _closed is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.Cut_ClosingDone, _closed.CutFolio, _closed.Folio);

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task CountAsync() => CountCoreAsync(keepNotice: false);

    [RelayCommand]
    private void Recount()
    {
        Notice = null;
        ErrorMessage = null;
        CommentError = null;
        Step = CloseShiftStep.Count;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmClose))]
    private async Task ConfirmCloseAsync()
    {
        if (_count is not { } count)
        {
            return;
        }

        ErrorMessage = null;
        CommentError = null;
        IsBusy = true;
        try
        {
            var command = new CloseShiftCommand(_shiftId, count.Version, count.CountedCents, count.ExpectedCents, Comment, _discardHeldSale);
            var (completed, result) = await _runner.RunAsync(
                "CerrarTurno",
                () => _useCases.RunAsync<CloseShiftHandler, Result<ClosedShift>>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["ShiftId"] = _shiftId });
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    _closed = result.Value;
                    OnPropertyChanged(nameof(DoneText));
                    Step = CloseShiftStep.Done;
                    await PrintReportCoreAsync(automatic: true);
                    break;

                case ShiftChanged or Conflict:
                    // El esperado o el turno cambiaron: se vuelven a mostrar las cifras (research §8).
                    await CountCoreAsync(keepNotice: true);
                    Notice = Strings.Shift_ChangedNotice;
                    break;

                case ValidationFailed validation:
                    CommentError = validation.Errors is [{ } first, ..] ? first.Message : Strings.Shift_CommentRequired;
                    break;

                case SaleInProgress:
                    await _dialogs.ShowMessageAsync(Strings.Shift_CloseTitle, CashShiftMessages.SaleInProgress);
                    _finished(false);
                    break;

                case ShiftClosed:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, CashShiftMessages.ShiftClosed);
                    _finished(true);
                    break;

                case Forbidden:
                    ErrorMessage = Strings.Common_Forbidden;
                    break;

                default:
                    ErrorMessage = Strings.Common_UnexpectedError;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task PrintReportAsync() => PrintReportCoreAsync(automatic: false);

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Finish() => _finished(true);

    [RelayCommand]
    private void Cancel() => _finished(_closed is not null);

    private bool CanAct() => !IsBusy;

    private bool CanConfirmClose() => !IsBusy && (!NeedsComment || !string.IsNullOrWhiteSpace(Comment));

    /// <summary>
    /// Corte Z impreso tras confirmar el cierre: una falla de impresión no revierte el cierre (research §12)
    /// y el corte se reimprime desde el histórico.
    /// </summary>
    private async Task PrintReportCoreAsync(bool automatic)
    {
        if (_closed is null)
        {
            return;
        }

        var wasBusy = IsBusy;
        IsBusy = true;
        try
        {
            await _printing.PrintAsync(PrintSource.ShiftCut(_closed.CutId), isReprint: !automatic, _closed.CutFolio, automatic);
        }
        finally
        {
            IsBusy = wasBusy;
        }
    }

    private async Task CountCoreAsync(bool keepNotice)
    {
        CountedError = null;
        ErrorMessage = null;
        if (!keepNotice)
        {
            Notice = null;
        }

        var parsed = Money.Parse(CountedText);
        if (parsed.Value is not { } counted)
        {
            CountedError = parsed.Error == MoneyParseError.TooLarge ? CashShiftMessages.AmountTooLarge : CashShiftMessages.CountedInvalid;
            return;
        }

        IsBusy = true;
        try
        {
            while (true)
            {
                var command = new CountShiftCashCommand(_shiftId, counted.Cents, _discardHeldSale);
                var (completed, result) = await _runner.RunAsync(
                    "ContarEfectivoDelTurno",
                    () => _useCases.RunAsync<CountShiftCashHandler, Result<ShiftCountResult>>(h => h.HandleAsync(command, CancellationToken.None)),
                    new Dictionary<string, object?> { ["ShiftId"] = _shiftId });
                if (!completed || result is null)
                {
                    return;
                }

                switch (result.Error)
                {
                    case null:
                        ApplyCount(result.Value);
                        return;

                    case HeldSaleWillBeDiscarded held:
                        var question = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Shift_HeldSaleQuestion,
                            CashShiftMessages.HeldSaleWillBeDiscarded(held.OwnerName));
                        if (!await _dialogs.AskAsync(Strings.Shift_HeldSaleTitle, question, Strings.Shift_HeldSaleContinue, Strings.Common_Cancel))
                        {
                            return;
                        }

                        _discardHeldSale = true;
                        continue;

                    case SaleInProgress:
                        await _dialogs.ShowMessageAsync(Strings.Shift_CloseTitle, CashShiftMessages.SaleInProgress);
                        _finished(false);
                        return;

                    case ShiftClosed:
                        await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, CashShiftMessages.ShiftClosed);
                        _finished(true);
                        return;

                    case NotFound:
                        await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, CashShiftMessages.ShiftClosed);
                        _finished(true);
                        return;

                    case ValidationFailed validation:
                        CountedError = validation.Errors is [{ } first, ..] ? first.Message : CashShiftMessages.CountedInvalid;
                        return;

                    case Forbidden:
                        ErrorMessage = Strings.Common_Forbidden;
                        return;

                    default:
                        ErrorMessage = Strings.Common_UnexpectedError;
                        return;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCount(ShiftCountResult count)
    {
        _count = count;
        ExpectedText = MoneyConverter.Format(count.ExpectedCents);
        CountedFigureText = MoneyConverter.Format(count.CountedCents);
        DifferenceText = MoneyConverter.Format(Math.Abs(count.DifferenceCents));
        DifferenceLabel = count.DifferenceKind switch
        {
            DifferenceKind.Over => Strings.Shift_Over,
            DifferenceKind.Shortage => Strings.Shift_Short,
            _ => Strings.Shift_Balanced,
        };
        CardText = MoneyConverter.Format(count.CardCents);
        TransferText = MoneyConverter.Format(count.TransferCents);
        NeedsComment = count.DifferenceCents != 0;
        ConfirmCloseCommand.NotifyCanExecuteChanged();
        Step = CloseShiftStep.Figures;
    }
}
