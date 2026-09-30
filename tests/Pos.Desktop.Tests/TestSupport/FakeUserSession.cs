using Pos.Application.Users;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Desktop.Tests.TestSupport;

/// <summary>Sesión fija de pruebas: por omisión un Administrador, para ver todo el menú y todas las tarjetas.</summary>
public sealed class FakeUserSession : IUserSession
{
    public FakeUserSession(UserRole role = UserRole.Admin) =>
        User = new SessionUser(FixedCurrentUser.Id, "Usuario de prueba", "prueba", role, "UP");

    public SessionUser? User { get; }

    public Guid UserId => FixedCurrentUser.Id;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}

/// <summary>Navegación de sesión de pruebas: no cambia de pantalla.</summary>
public sealed class FakeSessionNavigation : Pos.Desktop.Shell.ISessionNavigation
{
    public List<string> Requested { get; } = [];

    public Task<bool> NavigateAsync(string entryId)
    {
        Requested.Add(entryId);
        return Task.FromResult(true);
    }
}
