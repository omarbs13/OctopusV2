using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

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

    public CancelSaleViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        SaleDetailDto sale,
        Func<Task> finished,
        Action close)
    {
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

        var command = new CancelSaleCommand(_sale.Id, _sale.Version, Reason);
        var (completed, result) = await _runner.RunAsync(
            "CancelarVenta",
            () => _useCases.RunAsync<CancelSaleHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = _sale.Id });

        if (!completed || result is null)
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

            case Conflict:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.CancelSale_Changed);
                await _finished();
                break;

            default:
                ErrorMessage = Strings.Common_UnexpectedError;
                break;
        }
    }

    [RelayCommand]
    private void Close() => _close();
}
