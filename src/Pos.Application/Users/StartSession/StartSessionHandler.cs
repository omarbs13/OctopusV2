using Pos.Application.Abstractions;
using Pos.Application.Users.Session;

namespace Pos.Application.Users.StartSession;

/// <summary>Abre la sesión. Solo acepta al usuario que acaba de autenticarse con <c>SignIn</c>.</summary>
public sealed class StartSessionHandler
{
    private readonly UserSession _session;

    public StartSessionHandler(UserSession session) => _session = session;

    public Result Handle(SessionUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _session.TryStart(user)
            ? Result.Success()
            : Result.Failure(new InvalidState("El usuario no se autenticó."));
    }
}
