using Avalonia.Threading;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Serilog;

namespace Pos.Desktop.Composition;

/// <summary>
/// Última barrera ante errores no controlados. Las operaciones normales pasan por
/// <see cref="OperationRunner"/>; estos manejadores cubren lo que se escape.
/// </summary>
internal static class GlobalExceptionHandlers
{
    public static void Register(ILogger logger, Func<IDialogService?> dialogs)
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            logger.Error(e.Exception, "Excepción no controlada en el hilo de UI");
            e.Handled = true;
            var service = dialogs();
            if (service is not null)
            {
                _ = service.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error(e.Exception, "Excepción no observada en una tarea");
            e.SetObserved();
        };

        // En .NET no se puede evitar el cierre por una excepción en otro hilo; solo se registra.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.Fatal(e.ExceptionObject as Exception, "Excepción no controlada; el proceso terminará");
            Log.CloseAndFlush();
        };
    }
}
