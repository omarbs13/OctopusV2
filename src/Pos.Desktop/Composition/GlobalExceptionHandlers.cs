using Avalonia.Threading;
using Pos.Desktop.Common;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Resources;
using Serilog;

namespace Pos.Desktop.Composition;

/// <summary>
/// Última barrera ante errores no controlados. Las operaciones normales pasan por
/// <see cref="OperationRunner"/>; estos manejadores cubren lo que se escape. Toda excepción llega aquí
/// como FATAL con el contexto del diagnóstico, el operador ve un solo mensaje por episodio y la
/// aplicación sigue abierta (FR-001 a FR-004).
/// </summary>
internal static class GlobalExceptionHandlers
{
    public static void Register(
        ILogger logger,
        ErrorEpisodeGate gate,
        DiagnosticContext context,
        Func<IDialogService?> dialogs)
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Handle(logger, gate, context, dialogs, e.Exception, "Excepción no controlada en el hilo de UI");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Handle(logger, gate, context, dialogs, e.Exception, "Excepción no observada en una tarea asincrónica");
        };

        // En .NET no se puede evitar el cierre por una excepción en otro hilo; solo se registra.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                logger.Fatal(e.ExceptionObject as Exception, "Excepción no controlada; el proceso terminará");
                Log.CloseAndFlush();
            }
#pragma warning disable CA1031 // Ya no hay forma de informar nada si el propio registro falla.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        };
    }

    /// <summary>Registra, avisa al operador y se recupera; nunca lanza (FR-014).</summary>
    internal static void Handle(
        ILogger logger,
        ErrorEpisodeGate gate,
        DiagnosticContext context,
        Func<IDialogService?> dialogs,
        Exception exception,
        string message)
    {
        try
        {
            var observation = gate.Observe(exception);
            if (observation.IsNew)
            {
                logger.Fatal(exception, message);
            }

            if (observation.IsNew && gate.TryBeginNotification())
            {
                Dispatcher.UIThread.Post(() => _ = NotifyAsync(logger, gate, dialogs));
            }

            // Si la misma falla se repite, la pantalla actual no puede continuar: pantalla segura.
            if (observation.Count == 2)
            {
                Dispatcher.UIThread.Post(() => _ = ReturnToSafeScreenAsync(logger, context));
            }
        }
#pragma warning disable CA1031 // El manejador global no puede lanzar: sería la peor falla posible.
        catch (Exception)
#pragma warning restore CA1031
        {
            gate.EndNotification();
        }
    }

    private static async Task NotifyAsync(ILogger logger, ErrorEpisodeGate gate, Func<IDialogService?> dialogs)
    {
        try
        {
            var service = dialogs();
            if (service is not null)
            {
                await service.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
            }
        }
#pragma warning disable CA1031 // Si el diálogo falla, solo se registra.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.Error(ex, "No se pudo mostrar el mensaje de error al operador");
        }
        finally
        {
            gate.EndNotification();
        }
    }

    private static async Task ReturnToSafeScreenAsync(ILogger logger, DiagnosticContext context)
    {
        try
        {
            await context.TryReturnToSafeScreenAsync();
        }
#pragma warning disable CA1031 // La recuperación es de mejor esfuerzo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.Error(ex, "No se pudo regresar a la pantalla segura");
        }
    }
}
