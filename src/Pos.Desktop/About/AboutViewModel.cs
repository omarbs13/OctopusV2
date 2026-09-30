using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Diagnostics.GetAppInfo;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.About;

/// <summary>Pantalla "Acerca de": versión, carpeta de datos y exportación de diagnóstico (H6).</summary>
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
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _clock = clock;
        _clipboard = clipboard;
    }

    public override string Title => Strings.Shell_NavAbout;

    /// <summary>Puede exportar el diagnóstico (incluye una copia de la base); solo el Administrador.</summary>
    public bool CanExport { get; }

    [ObservableProperty]
    public partial string Version { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DataDirectory { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OperatingSystem { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExporting { get; private set; }

    public override async Task OnActivatedAsync()
    {
        var (completed, info) = await _runner.RunAsync(
            "ConsultarAcercaDe",
            () => _useCases.RunAsync<GetAppInfoHandler, AppInfoDto>(h => Task.FromResult(h.Handle())));
        if (completed && info is not null)
        {
            (Version, DataDirectory, OperatingSystem) = (info.Version, info.DataDirectory, info.OperatingSystem);
        }
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
                () => _useCases.RunAsync<ExportDiagnosticsHandler, Result<string>>(
                    h => h.HandleAsync(new ExportDiagnosticsCommand(destination), CancellationToken.None)),
                new Dictionary<string, object?> { ["Destination"] = destination });

            if (!completed || result is null)
            {
                return;
            }

            var message = result.IsSuccess
                ? string.Format(CultureInfo.CurrentCulture, Strings.About_ExportDone, result.Value)
                : result.Error is ExportFailed failed ? failed.Message : Strings.Common_UnexpectedError;
            await _dialogs.ShowMessageAsync(Strings.Shell_NavAbout, message);
        }
        finally
        {
            IsExporting = false;
        }
    }
}
