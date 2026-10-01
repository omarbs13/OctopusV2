using Pos.Application.Abstractions;
using Pos.Application.Reports.Export;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;

namespace Pos.Desktop.Reports;

/// <summary>
/// Flujo de exportación de las pantallas (contracts/ui.md): ejecuta <see cref="ExportReportHandler"/> en
/// segundo plano, abre "Guardar como" con el nombre sugerido, escribe el archivo y avisa. Si se cancela el
/// selector no pasa nada; si faltan los datos del negocio, ofrece ir a "Datos del negocio".
/// </summary>
public sealed class ReportExportCoordinator
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Navigator _navigator;

    public ReportExportCoordinator(UseCases useCases, OperationRunner runner, IDialogService dialogs, Navigator navigator)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _navigator = navigator;
    }

    /// <summary>Exporta y guarda; devuelve la ruta del archivo guardado, o nulo si no se guardó.</summary>
    public async Task<string?> ExportAsync(ExportRequest request, string periodText)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new Dictionary<string, object?>
        {
            ["Reporte"] = request.Kind.ToString(),
            ["Formato"] = request.Format.ToString(),
            ["Periodo"] = periodText,
        };

        var (completed, result) = await _runner.RunAsync(
            "ExportarReporte",
            () => _useCases.RunAsync<ExportReportHandler, Result<ExportedFile>>(h => h.HandleAsync(request, CancellationToken.None)),
            context);
        if (!completed || result is null)
        {
            return null;
        }

        if (!result.IsSuccess)
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, ErrorMessage(result.Error));
            return null;
        }

        var file = result.Value;
        var extension = Path.GetExtension(file.FileName).TrimStart('.');
        var path = await _dialogs.PickSaveFileAsync(Strings.Reports_SaveTitle, file.FileName, extension);
        if (path is null)
        {
            return null;
        }

        var (saved, _) = await _runner.RunAsync(
            "GuardarReporte",
            async () =>
            {
                await File.WriteAllBytesAsync(path, file.Bytes);
                return true;
            },
            context);
        if (!saved)
        {
            return null;
        }

        await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Reports_ExportSaved, path));
        if (file.BusinessMissing
            && await _dialogs.AskAsync(Strings.Reports_MissingBusinessTitle, Strings.Reports_MissingBusiness, Strings.Reports_GoToBusiness, Strings.Reports_Close))
        {
            await _navigator.NavigateAsync(SettingsModule.BusinessPageId);
        }

        return path;
    }

    private static string ErrorMessage(Error? error) => error switch
    {
        InvalidState state => state.Message,
        Forbidden => Strings.Reports_NoPermission,
        _ => Strings.Reports_ExportFailed,
    };
}
