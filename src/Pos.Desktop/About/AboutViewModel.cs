using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Common;
using Pos.Desktop.Licensing;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.About;

/// <summary>
/// Pantalla "Acerca de": producto, desarrollador y contacto, versión, ID de máquina, carpeta de datos,
/// exportación de diagnóstico y de respaldo (023, FR-030; 025). La licencia se administra en "Ayuda > Licencia".
/// </summary>
public sealed partial class AboutViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly IClipboardService _clipboard;
    private readonly Navigator? _navigator;

    public AboutViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        IClock clock,
        IClipboardService clipboard,
        ICurrentPermissions? permissions = null,
        Navigator? navigator = null)
    {
        CanExport = permissions?.Has(Permission.ExportDiagnostics) ?? true;
        CanExportBackup = permissions?.Has(Permission.ExportBackup) ?? false;
        _navigator = navigator;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _clock = clock;
        _clipboard = clipboard;
    }

    public override string Title => Strings.Shell_NavAbout;

    /// <summary>Puede exportar el diagnóstico (incluye una copia de la base); solo el Administrador.</summary>
    public bool CanExport { get; }

    /// <summary>Puede exportar un respaldo de la base (025); solo el Administrador.</summary>
    public bool CanExportBackup { get; }

    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DataDirectory { get; private set; } = string.Empty;

    /// <summary>ID de máquina para soporte; visible para todos los roles.</summary>
    [ObservableProperty]
    public partial string MachineId { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExporting { get; private set; }

    /// <summary>Incluye una copia de la base en el diagnóstico; por omisión no (FR-009).</summary>
    [ObservableProperty]
    public partial bool IncludeDatabase { get; set; }

    public override async Task OnActivatedAsync()
    {
        var (completed, info) = await _runner.RunAsync(
            "ConsultarAcercaDe",
            () => _useCases.RunAsync<GetAppInfoHandler, AppInfoDto>(h => Task.FromResult(h.Handle())));
        if (completed && info is not null)
        {
            (Version, DataDirectory) = (info.Version, info.DataDirectory);
        }

        await RefreshLicenseAsync();
    }

    [RelayCommand]
    private async Task CopyDataDirectoryAsync() =>
        await _runner.RunAsync("CopiarRutaDeDatos", () => _clipboard.SetTextAsync(DataDirectory));

    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = $"pos-diagnostico-{_clock.UtcNow.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.zip";
        var destination = await _dialogs.PickSaveFileAsync(Strings.About_ExportTitle, suggested, "zip");
        if (destination is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var (completed, result) = await _runner.RunAsync(
                "ExportarDiagnostico",
                () => _useCases.RunAsync<ExportDiagnosticsHandler, Result<ExportDiagnosticsResult>>(
                    h => h.HandleAsync(new ExportDiagnosticsCommand(destination, IncludeDatabase), CancellationToken.None)),
                new Dictionary<string, object?> { ["Destination"] = destination });

            if (!completed || result is null)
            {
                return;
            }

            var message = result.IsSuccess
                ? string.Format(CultureInfo.CurrentCulture, Strings.About_ExportDone, result.Value.DestinationFilePath)
                    + (result.Value.LogFileCount == 0 ? " " + Strings.About_ExportNoLogs : string.Empty)
                : result.Error is ExportFailed failed ? failed.Message : Strings.Common_UnexpectedError;
            await _dialogs.ShowMessageAsync(Strings.Shell_NavAbout, message);
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task RefreshLicenseAsync()
    {
        var status = await _useCases.RunAsync<GetLicenseStatusHandler, LicenseStatusDto>(h => Task.FromResult(h.Handle()));
        MachineId = status.MachineId;
    }

    /// <summary>Importar la licencia y generar la solicitud viven en "Ayuda > Licencia" (025).</summary>
    [RelayCommand]
    private async Task OpenLicenseAsync()
    {
        if (_navigator is not null)
        {
            await _navigator.NavigateAsync(LicenseModule.PageId);
        }
    }

    [RelayCommand]
    private Task ExportBackupAsync() => LicenseActions.ExportBackupAsync(_dialogs, _runner, _useCases, _clock, Strings.Shell_NavAbout);
}
