using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Application.Users.Access;

/// <summary>Verificación central de permisos (007, research §6).</summary>
public interface IAccessControl
{
    Task<AccessDecision> CheckAsync(Permission permission, CancellationToken cancellationToken);

    Task<AccessDecision> CheckAsync(Permission permission, Guid? authorizationGrantId, CancellationToken cancellationToken);

    /// <summary>
    /// Indica si el usuario conectado tiene el permiso, sin registrar rechazos ni consumir concesiones
    /// (008, research §7). Sirve para decidir qué tanto mostrar, no para autorizar.
    /// </summary>
    Task<bool> HasAsync(Permission permission, CancellationToken cancellationToken);
}

/// <summary>
/// Relee al usuario de la base en cada verificación: un usuario desactivado o con otro rol pierde
/// el acceso aunque su sesión siga abierta (FR-011). Registra los rechazos sin datos sensibles.
/// </summary>
public sealed partial class AccessControl : IAccessControl
{
    private readonly IUserSession _session;
    private readonly IUserRepository _users;
    private readonly IAuthorizationGrants _grants;
    private readonly ILogger<AccessControl> _logger;
    private readonly ILicenseState? _license;
    private readonly VendorContact _contact;

    public AccessControl(
        IUserSession session,
        IUserRepository users,
        IAuthorizationGrants grants,
        ILogger<AccessControl> logger,
        ILicenseState? license = null,
        VendorContact? contact = null)
    {
        _license = license;
        _contact = contact ?? VendorContact.Default;
        _session = session;
        _users = users;
        _grants = grants;
        _logger = logger;
    }

    public Task<AccessDecision> CheckAsync(Permission permission, CancellationToken cancellationToken) =>
        CheckAsync(permission, null, cancellationToken);

    public async Task<AccessDecision> CheckAsync(Permission permission, Guid? authorizationGrantId, CancellationToken cancellationToken)
    {
        // Modo lectura por licencia vencida (011): se bloquea antes de revisar roles.
        if (_license?.IsBlocked(permission) == true)
        {
            LogLicenseBlocked(_session.User?.Id, permission);
            return AccessDecision.Deny(new LicenseExpired(_contact.Phone, _contact.Email));
        }

        var sessionUser = _session.User;
        var user = sessionUser is null ? null : await _users.GetAsync(sessionUser.Id, cancellationToken);
        if (user is null || !user.IsActive || user.IsSystem)
        {
            LogDenied(sessionUser?.Id, permission);
            return AccessDecision.Deny(new Forbidden(permission, CanBeAuthorized: false));
        }

        if (RolePermissions.Has(user.Role, permission))
        {
            return AccessDecision.Allow();
        }

        if (authorizationGrantId is { } grantId
            && RolePermissions.IsAuthorizable(permission)
            && _grants.TryConsume(grantId, permission, user.Id) is { } authorizedBy)
        {
            return AccessDecision.Allow(authorizedBy);
        }

        LogDenied(user.Id, permission);
        return AccessDecision.Deny(new Forbidden(permission, RolePermissions.IsAuthorizable(permission)));
    }

    public async Task<bool> HasAsync(Permission permission, CancellationToken cancellationToken)
    {
        var sessionUser = _session.User;
        var user = sessionUser is null ? null : await _users.GetAsync(sessionUser.Id, cancellationToken);
        return user is { IsActive: true, IsSystem: false } && RolePermissions.Has(user.Role, permission);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operación rechazada por permisos. UserId={UserId} Permiso={Permission}")]
    private partial void LogDenied(Guid? userId, Permission permission);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operación bloqueada por licencia vencida. UserId={UserId} Permiso={Permission}")]
    private partial void LogLicenseBlocked(Guid? userId, Permission permission);
}
