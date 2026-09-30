using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Desktop.Common;

/// <summary>
/// Permisos del usuario conectado para ocultar botones y opciones. La protección real está en los
/// casos de uso (FR-011): esto solo evita ofrecer lo que se rechazaría.
/// </summary>
public interface ICurrentPermissions
{
    bool Has(Permission permission);
}

public sealed class SessionPermissions : ICurrentPermissions
{
    private readonly IUserSession _session;

    public SessionPermissions(IUserSession session) => _session = session;

    public bool Has(Permission permission) =>
        _session.User is { } user && RolePermissions.Has(user.Role, permission);
}
