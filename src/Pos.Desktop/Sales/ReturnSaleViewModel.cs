using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Returns;
using Pos.Application.Returns.PreviewReturn;
using Pos.Application.Returns.ReturnSaleItems;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.Common;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Sales;

/// <summary>Con qué se abre el formulario: toda la venta marcada (cancelar) o sin marcar (devolver artículos).</summary>
public enum ReturnFormMode
{
    Cancel,
    Items,
}

/// <summary>Línea de la venta en el formulario: casilla y cantidad a devolver.</summary>
public sealed partial class ReturnLineRow : ObservableObject
{
    private readonly Action _changed;

    public ReturnLineRow(SaleLineDto line, Action changed)
    {
        Line = line;
        _changed = changed;
    }

    public SaleLineDto Line { get; }

    public Guid Id => Line.Id;

    public string Name => Line.ProductName;

    public string Sku => Line.ProductSku;

    public long AvailableThousandths => Line.AvailableThousandths;

    public bool CanReturn => AvailableThousandths > 0;

    public string AvailableText => QuantityConverter.Format(AvailableThousandths, Line.DecimalPlaces);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string QuantityText { get; set; } = string.Empty;

    /// <summary>Milésimas capturadas; nulo si el texto no es una cantidad válida.</summary>
    public long? Thousandths => Quantity.Parse(QuantityText, Line.DecimalPlaces).Value?.Thousandths;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && Thousandths is not > 0)
        {
            SetQuantity(AvailableThousandths);
        }

        _changed();
    }

    partial void OnQuantityTextChanged(string value) => _changed();

    /// <summary>Captura la cantidad con los decimales de la unidad, sin separador de miles.</summary>
    public void SetQuantity(long thousandths) =>
        QuantityText = (thousandths / 1000m).ToString("F" + Math.Clamp(Line.DecimalPlaces, 0, 3), CultureInfo.InvariantCulture);
}

/// <summary>
/// Formulario "Devolver o cancelar" (contracts/ui.md §2): líneas y cantidades, motivo, compensación,
/// resumen calculado por <c>PreviewReturn</c> y, al confirmar, la autorización de un Administrador
/// (siempre, también si quien opera lo es). Sin el módulo Devoluciones es la cancelación básica de
/// siempre: solo motivo. Solo presenta: los montos los calcula el caso de uso (Principio III).
/// </summary>
public sealed partial class ReturnSaleViewModel : ViewModelBase
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly SaleDetailDto _sale;
    private readonly Func<Task> _finished;
    private readonly Action _close;
    private readonly AdminAuthorizationService? _authorization;
    private readonly TicketPrintingService? _printing;
    private readonly bool _isBasic;

    private ReturnPreview? _preview;
    private int _previewVersion;
    private bool _loading;

    public ReturnSaleViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        SaleDetailDto sale,
        ReturnFormMode mode,
        Func<Task> finished,
        Action close,
        AdminAuthorizationService? authorization = null,
        TicketPrintingService? printing = null,
        bool isBasic = false)
    {
        ArgumentNullException.ThrowIfNull(sale);
        _authorization = authorization;
        _printing = printing;
        _isBasic = isBasic;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _sale = sale;
        _finished = finished;
        _close = close;
        Mode = mode;

        _loading = true;
        foreach (var line in sale.Lines)
        {
            Lines.Add(new ReturnLineRow(line, OnLinesChanged));
        }

        if (mode == ReturnFormMode.Cancel && !isBasic)
        {
            SelectAllRows();
        }

        _loading = false;
        _ = RefreshPreviewAsync();
    }

    public ReturnFormMode Mode { get; }

    public string Folio => _sale.Folio;

    public string Title => Mode == ReturnFormMode.Cancel ? Strings.Return_TitleCancel : Strings.Return_TitleItems;

    public string Warning => _isBasic ? Strings.CancelSale_Warning : Strings.Return_Warning;

    public string ConfirmText => IsCancellation || _isBasic ? Strings.Return_ConfirmCancel : Strings.Return_ConfirmItems;

    /// <summary>Sin el módulo Devoluciones solo se muestra el motivo.</summary>
    public bool IsFullForm => !_isBasic;

    public ObservableCollection<ReturnLineRow> Lines { get; } = [];

    public ObservableCollection<string> Breakdown { get; } = [];

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreditNote))]
    public partial bool IsRefund { get; set; } = true;

    public bool IsCreditNote
    {
        get => !IsRefund;
        set => IsRefund = !value;
    }

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = Strings.Return_SummaryEmpty;

    [ObservableProperty]
    public partial string? CashWarning { get; private set; }

    /// <summary>Venta completa sin devoluciones previas con todo marcado: es una cancelación (FR-001).</summary>
    public bool IsCancellation =>
        _sale.ReturnedCents == 0
        && Lines.Count > 0
        && Lines.All(l => l.IsSelected && l.Thousandths == l.Line.QuantityThousandths);

    partial void OnIsRefundChanged(bool value) => _ = RefreshPreviewAsync();

    [RelayCommand]
    private void SelectAll()
    {
        _loading = true;
        SelectAllRows();
        _loading = false;
        OnLinesChanged();
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        ReasonError = null;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ReasonError = ReturnMessages.ReasonRequired;
            return;
        }

        if (_isBasic)
        {
            await ConfirmBasicAsync();
            return;
        }

        var requests = CurrentRequests(out var lineError);
        if (lineError || requests.Count == 0)
        {
            ErrorMessage = Strings.Return_NothingToReturn;
            return;
        }

        if (IsRefund && _preview is { CashRefundCents: > 0, CanRefundCash: false })
        {
            // Mensaje genérico: nunca se revela el efectivo esperado (008).
            ErrorMessage = Strings.Return_CashUnavailable;
            return;
        }

        if (_authorization is null || await _authorization.RequestAsync(Permission.ApproveReturns) is not { } grant)
        {
            // Sin autorización válida no se realiza ningún cambio (FR-002).
            return;
        }

        var compensation = IsRefund ? ReturnCompensation.Refund : ReturnCompensation.CreditNote;
        Result<ReturnResult>? result;
        if (IsCancellation)
        {
            var command = new CancelSaleCommand(_sale.Id, _sale.Version, Reason, grant, compensation);
            result = await RunAsync("CancelarVenta", h => h.HandleAsync(command, CancellationToken.None), grant);
        }
        else
        {
            var command = new ReturnSaleItemsCommand(_sale.Id, _sale.Version, requests, Reason, compensation, grant);
            result = await RunItemsAsync(command);
        }

        if (result is not null)
        {
            await ApplyResultAsync(result);
        }
    }

    /// <summary>Cancelación básica de 005/008 (sin el módulo Devoluciones): la autorización se pide solo si falta el permiso.</summary>
    private async Task ConfirmBasicAsync()
    {
        var result = await RunAsync(
            "CancelarVenta",
            h => h.HandleAsync(new CancelSaleCommand(_sale.Id, _sale.Version, Reason), CancellationToken.None),
            grant: null);
        if (result is { Error: Forbidden { CanBeAuthorized: true } } && _authorization is not null)
        {
            var request = await _dialogs.AskAsync(
                Strings.Auth_AuthorizeTitle,
                Strings.Auth_RequestAuthorizationQuestion,
                Strings.Auth_RequestAuthorization,
                Strings.Common_Cancel);
            if (!request || await _authorization.RequestAsync(Permission.CancelSales) is not { } grant)
            {
                return;
            }

            result = await RunAsync(
                "CancelarVenta",
                h => h.HandleAsync(new CancelSaleCommand(_sale.Id, _sale.Version, Reason, grant), CancellationToken.None),
                grant);
        }

        if (result is not null)
        {
            await ApplyResultAsync(result);
        }
    }

    private async Task<Result<ReturnResult>?> RunAsync(string name, Func<CancelSaleHandler, Task<Result<ReturnResult>>> call, Guid? grant)
    {
        var (completed, result) = await _runner.RunAsync(
            name,
            () => _useCases.RunAsync<CancelSaleHandler, Result<ReturnResult>>(call),
            new Dictionary<string, object?> { ["SaleId"] = _sale.Id, ["Authorized"] = grant is not null });
        return completed ? result : null;
    }

    private async Task<Result<ReturnResult>?> RunItemsAsync(ReturnSaleItemsCommand command)
    {
        var (completed, result) = await _runner.RunAsync(
            "DevolverArticulos",
            () => _useCases.RunAsync<ReturnSaleItemsHandler, Result<ReturnResult>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = _sale.Id, ["Lines"] = command.Lines.Count });
        return completed ? result : null;
    }

    private async Task ApplyResultAsync(Result<ReturnResult> result)
    {
        switch (result.Error)
        {
            case null:
                await OnSucceededAsync(result.Value);
                break;

            case ValidationFailed validation:
                var first = validation.Errors.Count > 0 ? validation.Errors[0] : null;
                if (first?.Field == ReturnFields.Reason || first?.Field == SaleFields.Reason)
                {
                    ReasonError = first.Message;
                }
                else
                {
                    ErrorMessage = first?.Message ?? Strings.Return_NothingToReturn;
                }

                break;

            case InvalidState invalid:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, invalid.Message);
                await _finished();
                break;

            case InsufficientCash:
                // Nunca se muestra el efectivo esperado: se sugiere nota de crédito o registrar un ingreso (008, FR-008).
                ErrorMessage = Strings.Return_CashUnavailable;
                break;

            case ShiftRequired:
                ErrorMessage = CashShiftMessages.ShiftRequired;
                break;

            case ShiftOwnedByOther other:
                ErrorMessage = CashShiftMessages.ShiftOwnedByOther(other.OpenedByName);
                break;

            case ReturnWindowExpired expired:
                ErrorMessage = string.Format(Display, Strings.Return_WindowExpired, expired.Days);
                break;

            case NothingToReturn:
                ErrorMessage = Strings.Return_NothingToReturn;
                break;

            case Conflict:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Return_Changed);
                await _finished();
                break;

            case Forbidden { CanBeAuthorized: true }:
                ErrorMessage = Strings.Return_NotAuthorized;
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;

            default:
                ErrorMessage = Strings.Common_UnexpectedError;
                break;
        }
    }

    private async Task OnSucceededAsync(ReturnResult result)
    {
        if (result.ReturnId != Guid.Empty)
        {
            await _dialogs.ShowMessageAsync(
                Strings.Common_InfoTitle,
                result.CreditNoteFolio is { } note
                    ? string.Format(Display, Strings.Return_DoneNote, result.Folio, note)
                    : string.Format(Display, Strings.Return_Done, result.Folio));
        }

        // El ticket de la nota se imprime al emitirla; una falla avisa con Reintentar y nunca deshace nada (research §13).
        if (result.CreditNoteId is { } noteId && result.CreditNoteFolio is { } noteFolio && _printing is not null)
        {
            await _printing.PrintAsync(PrintSource.CreditNote(noteId), isReprint: false, noteFolio, automatic: true);
        }

        await _finished();
    }

    [RelayCommand]
    private void Close() => _close();

    private void SelectAllRows()
    {
        foreach (var row in Lines.Where(l => l.CanReturn))
        {
            row.IsSelected = true;
            row.SetQuantity(row.AvailableThousandths);
        }
    }

    private void OnLinesChanged()
    {
        if (_loading)
        {
            return;
        }

        OnPropertyChanged(nameof(IsCancellation));
        OnPropertyChanged(nameof(ConfirmText));
        _ = RefreshPreviewAsync();
    }

    private List<ReturnLineRequest> CurrentRequests(out bool invalid)
    {
        invalid = false;
        var requests = new List<ReturnLineRequest>();
        foreach (var row in Lines.Where(l => l.IsSelected))
        {
            if (row.Thousandths is not > 0 || row.Thousandths > row.AvailableThousandths)
            {
                invalid = true;
                continue;
            }

            requests.Add(new ReturnLineRequest(row.Id, row.Thousandths.Value));
        }

        return requests;
    }

    /// <summary>Recalcula el monto y el reparto con el caso de uso antes de confirmar (FR-009).</summary>
    private async Task RefreshPreviewAsync()
    {
        if (_isBasic)
        {
            return;
        }

        var version = Interlocked.Increment(ref _previewVersion);
        var requests = CurrentRequests(out var invalid);
        if (invalid || requests.Count == 0)
        {
            _preview = null;
            SummaryText = Strings.Return_SummaryEmpty;
            CashWarning = null;
            Breakdown.Clear();
            return;
        }

        var command = new PreviewReturnCommand(_sale.Id, requests);
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "VistaPreviaDevolucion",
            () => _useCases.RunAsync<PreviewReturnHandler, Result<ReturnPreview>>(h => h.HandleAsync(command, CancellationToken.None)));
        if (!completed || result is null || version != _previewVersion)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            _preview = null;
            SummaryText = Strings.Return_SummaryEmpty;
            Breakdown.Clear();
            CashWarning = null;
            ErrorMessage = result.Error is ReturnWindowExpired expired
                ? string.Format(Display, Strings.Return_WindowExpired, expired.Days)
                : null;
            return;
        }

        _preview = result.Value;
        SummaryText = string.Format(Display, Strings.Return_Summary, MoneyConverter.Format(_preview.TotalCents));
        Breakdown.Clear();
        if (IsRefund)
        {
            foreach (var item in _preview.RefundBreakdown)
            {
                Breakdown.Add(string.Format(Display, BreakdownFormat(item.Method), MoneyConverter.Format(item.AmountCents)));
            }
        }

        CashWarning = IsRefund && _preview is { CashRefundCents: > 0, CanRefundCash: false } ? Strings.Return_CashUnavailable : null;
    }

    private static string BreakdownFormat(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => Strings.Return_BreakdownCash,
        PaymentMethod.Card => Strings.Return_BreakdownCard,
        PaymentMethod.Transfer => Strings.Return_BreakdownTransfer,
        _ => Strings.Return_BreakdownCredit,
    };
}
