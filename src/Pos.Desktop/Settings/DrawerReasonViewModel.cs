using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Pos.Desktop.Settings;

/// <summary>Diálogo del motivo para abrir el cajón sin venta (006, US5): el motivo vacío no abre y se pide de nuevo.</summary>
public sealed partial class DrawerReasonViewModel : Common.ViewModelBase
{
    private readonly TicketPrintingService _printing;
    private readonly Action<string, bool> _finished;
    private readonly Action _close;

    public DrawerReasonViewModel(TicketPrintingService printing, Action<string, bool> finished, Action close)
    {
        _printing = printing;
        _finished = finished;
        _close = close;
    }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ReasonError = null;
        IsBusy = true;
        try
        {
            var attempt = await _printing.OpenDrawerManualAsync(Reason);
            if (attempt.ReasonError is not null)
            {
                ReasonError = attempt.ReasonError;
                return;
            }

            _close();
            if (attempt.Message is not null)
            {
                _finished(attempt.Message, !attempt.Succeeded);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _close();

    private bool CanConfirm() => !IsBusy;
}
