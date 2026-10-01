using Pos.Application.Abstractions;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Resources;
using Serilog;

namespace Pos.Desktop.Common;

/// <summary>
/// Ejecuta las operaciones de la interfaz. Un error inesperado se registra con su contexto
/// (operación, usuario e identificadores), el operador ve un mensaje comprensible y la
/// aplicación sigue abierta (constitución, Principios I y VIII).
/// </summary>
public sealed class OperationRunner
{
    private readonly ILogger _logger;
    private readonly IDialogService _dialogs;
    private readonly ICurrentUser _currentUser;
    private readonly ErrorEpisodeGate? _gate;

    public OperationRunner(ILogger logger, IDialogService dialogs, ICurrentUser currentUser, ErrorEpisodeGate? gate = null)
    {
        _gate = gate;
        _logger = logger;
        _dialogs = dialogs;
        _currentUser = currentUser;
    }

    public async Task<bool> RunAsync(
        string operation,
        Func<Task> action,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var (ok, _) = await RunAsync(
            operation,
            async () =>
            {
                await action();
                return true;
            },
            context);
        return ok;
    }

    public async Task<(bool Succeeded, T? Value)> RunAsync<T>(
        string operation,
        Func<Task<T>> action,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return (true, await action());
        }
#pragma warning disable CA1031 // Toda falla inesperada se captura para que la aplicación no se cierre.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var log = ForOperation(operation, context);
            if (LogUnexpected(log, operation, ex))
            {
                await NotifyOperatorAsync(log);
            }

            return (false, default);
        }
    }

    /// <summary>
    /// Como <see cref="RunAsync"/>, pero sin diálogo: para operaciones de fondo (por ejemplo,
    /// una tarjeta de Inicio) cuyo error se muestra en su propio lugar.
    /// </summary>
    public async Task<bool> RunQuietlyAsync(
        string operation,
        Func<Task> action,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action();
            return true;
        }
#pragma warning disable CA1031 // Toda falla inesperada se captura y se registra.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnexpected(ForOperation(operation, context), operation, ex);
            return false;
        }
    }

    /// <summary>Como <see cref="RunQuietlyAsync"/>, pero devuelve el valor de la operación.</summary>
    public async Task<(bool Succeeded, T? Value)> RunQuietlyResultAsync<T>(
        string operation,
        Func<Task<T>> action,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return (true, await action());
        }
#pragma warning disable CA1031 // Toda falla inesperada se captura y se registra.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnexpected(ForOperation(operation, context), operation, ex);
            return (false, default);
        }
    }

    private ILogger ForOperation(string operation, IReadOnlyDictionary<string, object?>? context)
    {
        var log = _logger
            .ForContext("Operation", operation)
            .ForContext("UserId", _currentUser.UserId);

        if (context is not null)
        {
            foreach (var (key, value) in context)
            {
                log = log.ForContext(key, value);
            }
        }

        return log;
    }

    /// <summary>
    /// Registra la falla inesperada como FATAL; <c>false</c> si es una repetición dentro del episodio
    /// (solo se cuenta) o ya hay un aviso abierto, para no saturar al operador (FR-017).
    /// </summary>
    private bool LogUnexpected(ILogger log, string operation, Exception ex)
    {
        try
        {
            if (_gate is not null && !_gate.Observe(ex).IsNew)
            {
                return false;
            }

            log.Fatal(ex, "Error inesperado en la operación {Operation}", operation);
            return _gate?.TryBeginNotification() ?? true;
        }
#pragma warning disable CA1031 // Una falla del registro nunca debe afectar la operación (FR-014).
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private async Task NotifyOperatorAsync(ILogger log)
    {
        try
        {
            await _dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
        }
#pragma warning disable CA1031 // Si el diálogo falla, solo se registra.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            log.Error(ex, "No se pudo mostrar el mensaje de error al operador");
        }
        finally
        {
            _gate?.EndNotification();
        }
    }
}
