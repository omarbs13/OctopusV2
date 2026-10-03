using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Application.Licensing.ExportLicenseRequest;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Licensing.ImportLicense;
using Pos.Desktop.Common;
using Pos.Desktop.Licensing;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.About;

/// <summary>
/// Pantalla "Acerca de": producto, desarrollador y contacto, versión, ID de máquina, carpeta de datos,
/// exportación de diagnóstico y licencia (023, FR-030).
/// </summary>
public sealed partial class AboutViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly IClipboardService _clipboard;

    public AboutViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        IClock clock,
        IClipboardService clipboard,
        ICurrentPermissions? permissions = null)
    {
        CanExport = permissions?.Has(Permission.ExportDiagnostics) ?? true;
        CanManageLicense = permissions?.Has(Permission.ManageLicense) ?? false;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _clock = clock;
        _clipboard = clipboard;
    }

    public override string Title => Strings.Shell_NavAbout;

    /// <summary>Puede exportar el diagnóstico (incluye una copia de la base); solo el Administrador.</summary>
    public bool CanExport { get; }

    /// <summary>Puede importar licencias y exportar la solicitud (011); solo el Administrador.</summary>
    public bool CanManageLicense { get; }

    [ObservableProperty]
    public partial string LicenseSummary { get; private set; } = string.Empty;

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
        if (CanManageLicense)
        {
            LicenseSummary = LicenseMessages.Summary(status);
        }
    }

    [RelayCommand]
    private async Task ImportLicenseAsync()
    {
        var file = await _dialogs.PickOpenFileAsync(
            Strings.License_ImportTitle,
            [new FileTypeFilter(Strings.License_FileType, ["*.poslic"])]);
        if (file is null)
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync("ImportarLicencia", async () =>
        {
            // El selector entrega un flujo: se copia a un temporal para que el verificador lo lea por ruta.
            var temp = Path.Combine(Path.GetTempPath(), $"pos-lic-{Guid.NewGuid():N}.poslic");
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
            null => Strings.License_Imported,
            InvalidLicense invalid => LicenseMessages.Rejection(invalid.Reason),
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        };
        await _dialogs.ShowMessageAsync(Strings.License_AdminTitle, message);
        await RefreshLicenseAsync();
    }

    [RelayCommand]
    private async Task ExportLicenseRequestAsync()
    {
        var destination = await _dialogs.PickSaveFileAsync(Strings.License_ExportRequestTitle, "solicitud-licencia.posreq", "posreq");
        if (destination is null)
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "ExportarSolicitudLicencia",
            () => _useCases.RunAsync<ExportLicenseRequestHandler, Result<string>>(
                h => h.HandleAsync(new ExportLicenseRequestCommand(destination), CancellationToken.None)));
        if (!completed || result is null)
        {
            return;
        }

        var message = result.IsSuccess
            ? string.Format(CultureInfo.CurrentCulture, Strings.License_RequestSaved, result.Value)
            : result.Error is ExportFailed failed ? failed.Message : Strings.Common_UnexpectedError;
        await _dialogs.ShowMessageAsync(Strings.License_AdminTitle, message);
    }
}
