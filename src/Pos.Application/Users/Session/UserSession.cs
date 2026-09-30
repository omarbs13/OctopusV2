using Pos.Application.Abstractions;

namespace Pos.Application.Users.Session;

/// <summary>Sesión de la aplicación: usuario conectado actual. Sin sesión, el usuario es "Sistema".</summary>
public interface IUserSession : ICurrentUser
{
    SessionUser? User { get; }

    event EventHandler? Changed;
}

/// <summary>
/// Singleton en memoria que implementa el punto único del usuario conectado (Principio IV, FR-019).
/// La sesión solo se abre con el usuario que acaba de autenticarse (<see cref="Seal"/>).
/// </summary>
public sealed class UserSession : IUserSession
{
    private readonly Lock _gate = new();
    private SessionUser? _user;
    private SessionUser? _sealed;

    public event EventHandler? Changed;

    public SessionUser? User
    {
        get
        {
            lock (_gate)
            {
                return _user;
            }
        }
    }

    public Guid UserId => User?.Id ?? SystemUser.Id;

    /// <summary>Marca al usuario que acaba de autenticarse; solo él puede abrir la sesión.</summary>
    internal void Seal(SessionUser user)
    {
        lock (_gate)
        {
            _sealed = user;
        }
    }

    /// <summary>Indica si el usuario es el que acaba de autenticarse y aún no abre sesión (cambio obligatorio de contraseña).</summary>
    internal bool IsSealed(Guid userId)
    {
        lock (_gate)
        {
            return _sealed is not null && _sealed.Id == userId;
        }
    }

    /// <summary>Abre la sesión con el usuario sellado; <c>false</c> si no coincide con el último autenticado.</summary>
    internal bool TryStart(SessionUser user)
    {
        lock (_gate)
        {
            if (_sealed is null || _sealed.Id != user.Id)
            {
                return false;
            }

            _user = _sealed;
            _sealed = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    internal void End()
    {
        lock (_gate)
        {
            _user = null;
            _sealed = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
