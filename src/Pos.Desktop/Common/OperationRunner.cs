using Pos.Application.Abstractions;
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

    public OperationRunner(ILogger logger, IDialogService dialogs, ICurrentUser currentUser)
    {
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
            log.Error(ex, "Error inesperado en la operación {Operation}", operation);
            await NotifyOperatorAsync(log);
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
    }
}
