using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Purchases;
using Pos.Application.Purchases.VoidPurchase;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Purchases;

/// <summary>
/// Anular compra (contracts/ui.md "Detalle de compra"): motivo obligatorio y aviso. Si alguna línea no se puede
/// revertir muestra cada producto con su causa (escenario 14); un conflicto de versión recarga el detalle.
/// </summary>
public sealed partial class VoidPurchaseViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly PurchaseDetailDto _purchase;
    private readonly Func<Task> _finished;
    private readonly Func<Task> _conflict;
    private readonly Action _closed;

    public VoidPurchaseViewModel(
        UseCases useCases,
        OperationRunner runner,
        PurchaseDetailDto purchase,
        Func<Task> finished,
        Func<Task> conflict,
        Action closed)
    {
        _useCases = useCases;
        _runner = runner;
        _purchase = purchase;
        _finished = finished;
        _conflict = conflict;
        _closed = closed;
    }

    /// <summary>Productos que impiden la anulación, con su causa.</summary>
    public ObservableCollection<string> BlockedLines { get; } = [];

    [ObservableProperty]
    public partial bool IsBlocked { get; private set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; private set; }

    private bool CanConfirm() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ReasonError = ErrorMessage = null;
        BlockedLines.Clear();
        IsBlocked = false;
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ReasonError = PurchaseMessages.ReasonRequired;
            return;
        }

        IsBusy = true;
        try
        {
            var command = new VoidPurchaseCommand(_purchase.Id, _purchase.Version, Reason);
            var (completed, result) = await _runner.RunAsync(
                "AnularCompra",
                () => _useCases.RunAsync<VoidPurchaseHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["PurchaseId"] = _purchase.Id });
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    await _finished();
                    break;
                case PurchaseVoidBlocked blocked:
                    foreach (var line in blocked.Lines)
                    {
                        var decimals = _purchase.Lines.FirstOrDefault(l => l.ProductId == line.ProductId)?.DecimalPlaces ?? 3;
                        BlockedLines.Add(PurchaseMessages.ForBlocker(
                            line.ProductName,
                            line.Reason,
                            QuantityConverter.Format(line.OnHandThousandths, decimals),
                            QuantityConverter.Format(line.RequiredThousandths, decimals)));
                    }

                    IsBlocked = true;
                    ErrorMessage = Strings.Purchase_VoidBlocked;
                    break;
                case Conflict:
                    await _conflict();
                    break;
                case InvalidState:
                    ErrorMessage = Strings.Purchase_AlreadyVoided;
                    break;
                case ValidationFailed validation:
                    ReasonError = validation.Errors is [{ } first, ..] ? first.Message : Strings.Common_UnexpectedError;
                    break;
                case ModuleNotLicensed:
                    ErrorMessage = Strings.License_ModuleNotLicensed;
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

    [RelayCommand]
    private void Close() => _closed();
}
