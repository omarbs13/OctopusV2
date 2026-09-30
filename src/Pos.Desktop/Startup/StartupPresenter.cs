using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Startup;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Serilog;

namespace Pos.Desktop.Startup;

/// <summary>
/// Ejecuta el arranque de la base y traduce su resultado a mensajes para el operador
/// (contracts/startup.md). Devuelve verdadero si la aplicación puede abrir la ventana principal.
/// </summary>
public sealed class StartupPresenter
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    private readonly IDatabaseStartup _startup;
    private readonly IDialogService _dialogs;
    private readonly IAppPaths _paths;
    private readonly ILogger _logger;

    public StartupPresenter(IDatabaseStartup startup, IDialogService dialogs, IAppPaths paths, ILogger logger)
    {
        _startup = startup;
        _dialogs = dialogs;
        _paths = paths;
        _logger = logger;
    }

    public async Task<bool> RunAsync(CancellationToken cancellationToken, IProgress<StartupStep>? progress = null)
    {
        try
        {
            while (true)
            {
                var result = await _startup.RunAsync(cancellationToken, progress);
                switch (result)
                {
                    case StartupResult.Ready:
                        return true;

                    case StartupResult.Corrupted { LatestBackup: { } backup }:
                        if (!await ConfirmRestoreAsync(backup))
                        {
                            await ShowAsync(Strings.Startup_ContactSupport);
                            return false;
                        }

                        await _startup.RecoverFromBackupAsync(backup, cancellationToken, progress);
                        continue;

                    default:
                        await ShowAsync(MessageFor(result));
                        return false;
                }
            }
        }
#pragma warning disable CA1031 // Una falla inesperada al arrancar se informa sin cerrar abruptamente.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.ForContext("Operation", "Arranque").Error(ex, "Error inesperado durante el arranque");
            await ShowAsync(Strings.Common_UnexpectedError);
            return false;
        }
    }

    private string MessageFor(StartupResult result) => result switch
    {
        StartupResult.NewerDatabase => Strings.Startup_NewerDatabase,
        StartupResult.MigrationFailed => Strings.Startup_MigrationFailed,
        StartupResult.InsufficientSpace => Strings.Startup_InsufficientSpace,
        StartupResult.Inaccessible { Reason: DatabaseProblem.Locked } => Strings.Startup_Locked,
        StartupResult.Inaccessible { Reason: DatabaseProblem.DiskFull } => Strings.Startup_InsufficientSpace,
        StartupResult.Inaccessible => string.Format(CultureInfo.CurrentCulture, Strings.Startup_PermissionDenied, _paths.DataDirectory),
        StartupResult.Corrupted => Strings.Startup_CorruptedNoBackup,
        _ => Strings.Common_UnexpectedError,
    };

    private Task<bool> ConfirmRestoreAsync(BackupInfo backup)
    {
        var date = backup.CreatedAtUtc.ToLocalTime().ToString("g", DisplayCulture);
        var message = string.Format(CultureInfo.CurrentCulture, Strings.Startup_CorruptedConfirm, date);
        return _dialogs.ConfirmAsync(Strings.Startup_Title, message, Strings.Startup_RestoreButton);
    }

    private Task ShowAsync(string message) => _dialogs.ShowMessageAsync(Strings.Startup_Title, message);
}
