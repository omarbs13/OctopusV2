using Microsoft.Extensions.DependencyInjection;

namespace Pos.Desktop.Common;

/// <summary>
/// Invoca un caso de uso dentro de su propio ámbito de dependencias, de modo que cada operación
/// usa una unidad de trabajo (DbContext) nueva.
/// </summary>
public sealed class UseCases
{
    private readonly IServiceScopeFactory _scopes;

    public UseCases(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<TResult> RunAsync<THandler, TResult>(Func<THandler, Task<TResult>> call)
        where THandler : notnull
    {
        ArgumentNullException.ThrowIfNull(call);
        await using var scope = _scopes.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<THandler>());
    }
}
