using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Security;
using Pos.Application.Security.GetSecuritySettings;
using Pos.Application.Security.SaveSecuritySettings;
using Pos.Application.Users;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;

namespace Pos.Desktop.Settings;

/// <summary>Configuración › Seguridad: tiempo de inactividad antes de bloquear la sesión (FR-023).</summary>
public sealed partial class SecuritySettingsViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IdleMonitor? _idle;

    public SecuritySettingsViewModel(UseCases useCases, OperationRunner runner, IdleMonitor? idle = null)
    {
        _useCases = useCases;
        _runner = runner;
        _idle = idle;
    }

    public override string Title => Strings.SecuritySettings_Title;

    /// <summary>Bloqueo por inactividad activado; desactivado guarda 0.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string MinutesText { get; set; } = SecuritySettings.DefaultIdleLockMinutes.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial string? MinutesError { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; private set; }

    public override async Task OnActivatedAsync()
    {
        StatusMessage = null;
        MinutesError = null;
        var (completed, result) = await _runner.RunAsync(
            "CargarConfiguracionDeSeguridad",
            () => _useCases.RunAsync<GetSecuritySettingsHandler, Result<SecuritySettings>>(h => Task.FromResult(h.Handle())));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var minutes = result.Value.IdleLockMinutes;
        IsEnabled = minutes > 0;
        MinutesText = (minutes > 0 ? minutes : SecuritySettings.DefaultIdleLockMinutes).ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        MinutesError = null;
        StatusMessage = null;

        var minutes = 0;
        if (IsEnabled
            && (!int.TryParse(MinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out minutes)
                || minutes is < 1 or > SecuritySettings.MaxIdleLockMinutes))
        {
            MinutesError = UserMessages.IdleLockRange;
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new SecuritySettings { IdleLockMinutes = minutes };
            var (completed, result) = await _runner.RunAsync(
                "GuardarConfiguracionDeSeguridad",
                () => _useCases.RunAsync<SaveSecuritySettingsHandler, Result>(h => h.HandleAsync(settings, CancellationToken.None)));
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    _idle?.Start(minutes);
                    StatusMessage = Strings.SecuritySettings_Saved;
                    break;

                case ValidationFailed validation:
                    MinutesError = validation.Errors.Count > 0 ? validation.Errors[0].Message : UserMessages.IdleLockRange;
                    break;

                default:
                    StatusMessage = Strings.Common_Forbidden;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => !IsBusy;
}
