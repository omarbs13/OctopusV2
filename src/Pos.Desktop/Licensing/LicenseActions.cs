using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Backup.ExportBackup;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Licensing;

/// <summary>Acciones compartidas por "Ayuda > Licencia" y "Acerca de" (025).</summary>
internal static class LicenseActions
{
    /// <summary>Exporta un respaldo completo de la base; disponible también en bloqueo (research §11).</summary>
    public static async Task ExportBackupAsync(IDialogService dialogs, OperationRunner runner, UseCases useCases, IClock clock, string title)
    {
        var suggested = $"pos-respaldo-{clock.UtcNow.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.db";
        var destination = await dialogs.PickSaveFileAsync(Strings.License_ExportBackupTitle, suggested, "db");
        if (destination is null)
        {
            return;
        }

        var (completed, result) = await runner.RunAsync(
            "ExportarRespaldo",
            () => useCases.RunAsync<ExportBackupHandler, Result<string>>(
                h => h.HandleAsync(new ExportBackupCommand(destination), CancellationToken.None)),
            new Dictionary<string, object?> { ["Destination"] = destination });
        if (!completed || result is null)
        {
            return;
        }

        var message = result.Error switch
        {
            null => string.Format(CultureInfo.CurrentCulture, Strings.License_BackupSaved, result.Value),
            ExportFailed failed => failed.Message,
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        };
        await dialogs.ShowMessageAsync(title, message);
    }
}
