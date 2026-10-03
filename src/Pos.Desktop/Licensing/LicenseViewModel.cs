using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Licensing.ExportLicenseRequest;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Licensing.ImportLicense;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Licensing;

/// <summary>Fila de la tabla de módulos (FR-040).</summary>
public sealed record LicenseModuleRow(string Name, string State, string ActivatesOn, string ExpiresOn);

/// <summary>
/// "Ayuda > Licencia" (025, FR-013 a FR-017, FR-040, contracts/blocked-mode.md §4): ID de máquina con "Copiar",
/// estado general, cliente, avisos y la tabla de los 9 módulos; generar solicitud (cualquier usuario),
/// importar licencia y exportar respaldo (Administrador). Disponible siempre, también en bloqueo.
/// </summary>
public sealed partial class LicenseViewModel : PageViewModel, IDisposable
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly IClock _clock;
    private readonly ILicenseState? _license;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    public LicenseViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        IClipboardService clipboard,
        IClock clock,
        ICurrentPermissions? permissions = null,
        ILicenseState? license = null)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _clock = clock;
        _license = license;
        CanImport = permissions?.Has(Permission.ManageLicense) ?? false;
        CanExportBackup = permissions?.Has(Permission.ExportBackup) ?? false;
        if (_license is not null)
        {
            _license.Changed += OnLicenseChanged;
        }
    }

    public override string Title => Strings.Nav_License;

    /// <summary>Importar licencia: solo con <see cref="Permission.ManageLicense"/>.</summary>
    public bool CanImport { get; }

    /// <summary>Exportar respaldo: solo con <see cref="Permission.ExportBackup"/>.</summary>
    public bool CanExportBackup { get; }

    [ObservableProperty]
    public partial string MachineId { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OverallText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomerText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCustomer { get; private set; }

    /// <summary>Mensaje de activación con los pasos; solo en bloqueo.</summary>
    [ObservableProperty]
    public partial string BlockedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBlocked { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LicenseModuleRow> Modules { get; private set; } = [];

    public override Task OnActivatedAsync() => RefreshAsync();

    public void Dispose()
    {
        if (_license is not null)
        {
            _license.Changed -= OnLicenseChanged;
        }
    }

    [RelayCommand]
    private async Task CopyMachineIdAsync()
    {
        var (completed, _) = await _runner.RunAsync("CopiarIdMaquina", async () =>
        {
            await _clipboard.SetTextAsync(MachineId);
            return true;
        });
        if (completed)
        {
            await _dialogs.ShowMessageAsync(Strings.Nav_License, Strings.License_Copied);
        }
    }

    [RelayCommand]
    private async Task GenerateRequestAsync()
    {
        var destination = await _dialogs.PickSaveFileAsync(Strings.License_GenerateRequestTitle, "solicitud-licencia.octoreq", "octoreq");
        if (destination is null)
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "GenerarSolicitudLicencia",
            () => _useCases.RunAsync<ExportLicenseRequestHandler, Result<string>>(
                h => h.HandleAsync(new ExportLicenseRequestCommand(destination), CancellationToken.None)));
        if (!completed || result is null)
        {
            return;
        }

        var message = result.Error switch
        {
            null => string.Format(CultureInfo.CurrentCulture, Strings.License_RequestSaved, result.Value),
            ExportFailed failed => failed.Message,
            _ => Strings.Common_UnexpectedError,
        };
        await _dialogs.ShowMessageAsync(Strings.Nav_License, message);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var file = await _dialogs.PickOpenFileAsync(
            Strings.License_ImportTitle,
            [new FileTypeFilter(Strings.License_FileType, ["*.lic"])]);
        if (file is null)
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync("ImportarLicencia", async () =>
        {
            // El selector entrega un flujo: se copia a un temporal para leerlo por ruta.
            var temp = Path.Combine(Path.GetTempPath(), $"pos-lic-{Guid.NewGuid():N}.lic");
            try
            {
                await using (var source = await file.OpenAsync())
                await using (var target = File.Create(temp))
                {
                    await source.CopyToAsync(target);
                }

                return await _useCases.RunAsync<ImportLicenseHandler, Result<LicenseStatusDto>>(
                    h => h.HandleAsync(new ImportLicenseCommand(temp), CancellationToken.None));
            }
            finally
            {
                File.Delete(temp);
            }
        });
        if (!completed || result is null)
        {
            return;
        }

        var message = result.Error switch
        {
            null => LicenseMessages.Imported(result.Value),
            InvalidLicense invalid => LicenseMessages.Rejection(invalid.Reason),
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        };
        await _dialogs.ShowMessageAsync(Strings.Nav_License, message);
        await RefreshAsync();
    }

    [RelayCommand]
    private Task ExportBackupAsync() => LicenseActions.ExportBackupAsync(_dialogs, _runner, _useCases, _clock, Strings.Nav_License);

    private async Task RefreshAsync()
    {
        var status = await _useCases.RunAsync<GetLicenseStatusHandler, LicenseStatusDto>(h => Task.FromResult(h.Handle()));
        MachineId = status.MachineId;
        OverallText = LicenseMessages.Overall(status);
        HasCustomer = !string.IsNullOrEmpty(status.CustomerName);
        CustomerText = HasCustomer ? string.Format(CultureInfo.CurrentCulture, Strings.License_Customer, status.CustomerName) : string.Empty;
        IsBlocked = status.IsBlocked;
        BlockedText = status.IsBlocked ? $"{LicenseMessages.Blocked(status)} {LicenseMessages.Contact(status)}" : string.Empty;

        var warnings = new List<string>();
        if (status.ClockBehind)
        {
            warnings.Add(LicenseMessages.ClockBehind(status));
        }

        switch (status.TrialWarning)
        {
            case Domain.Licensing.LicenseWarning.Near:
                warnings.Add(Strings.License_NearExpiry);
                break;
            case Domain.Licensing.LicenseWarning.Urgent:
                warnings.Add(Strings.License_NearExpiryOne);
                break;
        }

        warnings.AddRange(status.ExpiringSoon.Select(LicenseMessages.Expiring));
        Warnings = warnings;

        Modules = [.. status.Modules.Select(m => new LicenseModuleRow(
            m.Name,
            LicenseMessages.State(m.State),
            m.ActivatesOn is { } activates ? LicenseMessages.DateText(activates) : string.Empty,
            m.ActivatesOn is null ? string.Empty : LicenseMessages.DateText(m.ExpiresOn)))];
    }

    private void OnLicenseChanged(object? sender, EventArgs e)
    {
        if (_context is null)
        {
            _ = RefreshQuietlyAsync();
        }
        else
        {
            _context.Post(_ => _ = RefreshQuietlyAsync(), null);
        }
    }

    private Task<(bool Succeeded, bool Value)> RefreshQuietlyAsync() => _runner.RunQuietlyResultAsync("ActualizarLicencia", async () =>
    {
        await RefreshAsync();
        return true;
    });
}
