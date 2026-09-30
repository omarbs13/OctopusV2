using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Sales;

/// <summary>Formulario "Cancelar venta": motivo obligatorio y confirmación de que se regresan las existencias.</summary>
public sealed partial class CancelSaleViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly SaleDetailDto _sale;
    private readonly Func<Task> _finished;
    private readonly Action _close;
    private readonly AdminAuthorizationService? _authorization;

    public CancelSaleViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        SaleDetailDto sale,
        Func<Task> finished,
        Action close,
        AdminAuthorizationService? authorization = null)
    {
        _authorization = authorization;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _sale = sale;
        _finished = finished;
        _close = close;
    }

    public string Folio => _sale.Folio;

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        ReasonError = null;
        ErrorMessage = null;

        var result = await CancelAsync(grantId: null);
        if (result is { Error: Forbidden { CanBeAuthorized: true } } && _authorization is not null)
        {
            // Sin el permiso: se ofrece la autorización de un administrador sin cerrar la sesión (FR-013).
            var request = await _dialogs.AskAsync(
                Strings.Auth_AuthorizeTitle,
                Strings.Auth_RequestAuthorizationQuestion,
                Strings.Auth_RequestAuthorization,
                Strings.Common_Cancel);
            if (!request || await _authorization.RequestAsync(Permission.CancelSales) is not { } grant)
            {
                return;
            }

            result = await CancelAsync(grant);
        }

        if (result is null)
        {
            return;
        }

        switch (result.Error)
        {
            case null:
                await _finished();
                break;

            case ValidationFailed validation:
                ReasonError = validation.Errors.Count > 0 ? validation.Errors[0].Message : null;
                break;

            case InvalidState invalid:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, invalid.Message);
                await _finished();
                break;

            case InsufficientCash:
                // Nunca se muestra el efectivo esperado: se sugiere registrar un ingreso y reintentar (008, FR-008).
                ErrorMessage = CashShiftMessages.RefundExceedsCash;
                break;

            case Conflict:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.CancelSale_Changed);
                await _finished();
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            default:
                ErrorMessage = Strings.Common_UnexpectedError;
                break;
        }
    }

    private async Task<Result?> CancelAsync(Guid? grantId)
    {
        var command = new CancelSaleCommand(_sale.Id, _sale.Version, Reason, grantId);
        var (completed, result) = await _runner.RunAsync(
            "CancelarVenta",
            () => _useCases.RunAsync<CancelSaleHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = _sale.Id, ["Authorized"] = grantId is not null });
        return completed ? result : null;
    }

    [RelayCommand]
    private void Close() => _close();
}
