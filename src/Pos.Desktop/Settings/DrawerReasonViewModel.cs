using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Settings;

/// <summary>Diálogo del motivo para abrir el cajón sin venta (006, US5): el motivo vacío no abre y se pide de nuevo.</summary>
public sealed partial class DrawerReasonViewModel : Common.ViewModelBase
{
    private readonly TicketPrintingService _printing;
    private readonly Action<string, bool> _finished;
    private readonly Action _close;
    private readonly AdminAuthorizationService? _authorization;
    private readonly IDialogService? _dialogs;

    public DrawerReasonViewModel(
        TicketPrintingService printing,
        Action<string, bool> finished,
        Action close,
        AdminAuthorizationService? authorization = null,
        IDialogService? dialogs = null)
    {
        _authorization = authorization;
        _dialogs = dialogs;
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
            if (attempt.CanAuthorize && _authorization is not null && _dialogs is not null)
            {
                // Sin el permiso: se ofrece la autorización de un administrador sin cerrar la sesión (FR-013).
                var request = await _dialogs.AskAsync(
                    Strings.Auth_AuthorizeTitle,
                    Strings.Auth_RequestAuthorizationQuestion,
                    Strings.Auth_RequestAuthorization,
                    Strings.Common_Cancel);
                if (!request || await _authorization.RequestAsync(Permission.OpenDrawerWithoutSale) is not { } grant)
                {
                    return;
                }

                attempt = await _printing.OpenDrawerManualAsync(Reason, grant);
            }

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
