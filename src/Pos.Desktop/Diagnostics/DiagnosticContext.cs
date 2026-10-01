using Pos.Application.Users.Session;

namespace Pos.Desktop.Diagnostics;

/// <summary>Instantánea inmutable de lo que el operador estaba haciendo cuando se escribe una entrada del log.</summary>
public sealed record DiagnosticSnapshot(
    Guid? UserId,
    string? UserName,
    string? Screen,
    Guid? SaleDraftId,
    int? SaleLines);

/// <summary>
/// Contexto ambiental del diagnóstico: usuario conectado, pantalla actual y venta en curso. Lo leen
/// los enriquecedores de Serilog al escribir, de modo que ningún caso de uso tiene que pasarlo.
/// Es un singleton seguro entre hilos; cada dato se publica como un valor inmutable.
/// </summary>
public sealed class DiagnosticContext
{
    private readonly Lock _gate = new();
    private IUserSession? _session;
    private string? _screen;
    private Guid? _saleDraftId;
    private int? _saleLines;
    private Func<Task>? _returnToSafeScreen;

    /// <summary>Conecta la sesión para conocer al usuario conectado (se resuelve al componer la aplicación).</summary>
    public void BindSession(IUserSession session)
    {
        lock (_gate)
        {
            _session = session;
        }
    }

    public void SetScreen(string? screen)
    {
        lock (_gate)
        {
            _screen = screen;
        }
    }

    /// <summary>Publica la venta sin guardar; <paramref name="lines"/> en cero o nulo indica que no hay venta en curso.</summary>
    public void SetSale(Guid? draftId, int? lines)
    {
        lock (_gate)
        {
            var any = lines is > 0;
            _saleDraftId = any ? draftId : null;
            _saleLines = any ? lines : null;
        }
    }

    /// <summary>Acción que lleva al operador a la pantalla segura (Punto de venta); la registra la navegación de la sesión.</summary>
    public void SetSafeScreenAction(Func<Task>? action)
    {
        lock (_gate)
        {
            _returnToSafeScreen = action;
        }
    }

    public DiagnosticSnapshot Current
    {
        get
        {
            IUserSession? session;
            lock (_gate)
            {
                session = _session;
                var screen = _screen;
                var draft = _saleDraftId;
                var lines = _saleLines;
                var user = session?.User;
                
                // Sin sesión no hay pantalla de navegación ni venta en curso que describir.
                return user is null
                    ? new DiagnosticSnapshot(null, null, null, null, null)
                    : new DiagnosticSnapshot(user.Id, user.UserName, screen, draft, lines);
            }
        }
    }

    /// <summary>Lleva al operador a la pantalla segura si la sesión actual la registró; <c>false</c> si no se pudo.</summary>
    public async Task<bool> TryReturnToSafeScreenAsync()
    {
        Func<Task>? action;
        lock (_gate)
        {
            action = _returnToSafeScreen;
        }

        if (action is null)
        {
            return false;
        }

        await action();
        return true;
    }
}
