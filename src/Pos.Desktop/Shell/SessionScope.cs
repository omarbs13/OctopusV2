using Microsoft.Extensions.DependencyInjection;

namespace Pos.Desktop.Shell;

/// <summary>
/// Ámbito de dependencias de una sesión (007, research §12): pantallas, menú y tarjetas son de la
/// sesión, así que al desecharla la siguiente empieza limpia y con el menú de su rol.
/// </summary>
public sealed class SessionScope : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;

    public SessionScope(IServiceProvider root)
    {
        _scope = root.CreateAsyncScope();
        Main = _scope.ServiceProvider.GetRequiredService<MainViewModel>();
    }

    public MainViewModel Main { get; }

    /// <summary>Servicio del ámbito de la sesión (por ejemplo, un monitor que se detiene al desecharla).</summary>
    public T GetRequiredService<T>()
        where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
