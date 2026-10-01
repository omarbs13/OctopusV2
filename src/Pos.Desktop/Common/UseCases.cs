using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Serilog;

namespace Pos.Desktop.Common;

/// <summary>
/// Invoca un caso de uso dentro de su propio ámbito de dependencias, de modo que cada operación
/// usa una unidad de trabajo (DbContext) nueva. Es el único punto por el que pasan los casos de uso
/// desde la interfaz, así que aquí se registran sus fallas controladas (nivel ERROR) sin tocar los
/// casos de uso: solo el tipo de falla y los nombres de campo, nunca los valores ingresados.
/// </summary>
public sealed class UseCases
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger? _logger;

    public UseCases(IServiceScopeFactory scopes, ILogger? logger = null)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task<TResult> RunAsync<THandler, TResult>(Func<THandler, Task<TResult>> call)
        where THandler : notnull
    {
        ArgumentNullException.ThrowIfNull(call);
        await using var scope = _scopes.CreateAsyncScope();
        var result = await call(scope.ServiceProvider.GetRequiredService<THandler>());
        LogFailure(typeof(THandler).Name, result as Result);
        return result;
    }

    /// <summary>
    /// Flujos esperados del negocio (credenciales, permisos, ventas que cambiaron) no son errores: los
    /// casos de uso ya los registran como WARNING o INFO.
    /// </summary>
    private static bool IsExpected(Error error) =>
        error is Forbidden or InvalidCredentials or LockedOut or AlreadyRegistered or SaleChanged
            or ShiftRequired or ShiftOwnedByOther or ShiftAlreadyOpen or ShiftClosed or ShiftChanged
            or InsufficientCash or SaleInProgress or HeldSaleWillBeDiscarded or ModuleNotLicensed
            or ReturnWindowExpired or NothingToReturn or CreditNoteNotFound or InsufficientCreditNote;

    private void LogFailure(string handler, Result? result)
    {
        if (_logger is null || result is not { IsSuccess: false, Error: { } error } || IsExpected(error))
        {
            return;
        }

        try
        {
            var fields = error is ValidationFailed validation
                ? string.Join(", ", validation.Errors.Select(e => e.Field))
                : null;
            _logger.Error(
                "El caso de uso {UseCase} terminó con un error controlado: {ErrorType} {Fields}",
                handler,
                error.GetType().Name,
                fields);
        }
#pragma warning disable CA1031 // Una falla del registro nunca debe afectar la operación (FR-014).
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
