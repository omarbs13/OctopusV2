using System.Collections.Concurrent;
using Pos.Application.Abstractions;
using Pos.Domain.Users;

namespace Pos.Application.Users.Access;

/// <summary>Concesiones de autorización de administrador en memoria (007, research §8).</summary>
public interface IAuthorizationGrants
{
    Guid Issue(Permission permission, Guid requestedBy, Guid authorizedBy);

    /// <summary>Consume la concesión si coincide el permiso y el solicitante y no venció; devuelve el administrador.</summary>
    Guid? TryConsume(Guid grantId, Permission permission, Guid requestedBy);
}

/// <summary>Concesión de un solo uso, ligada a permiso y solicitante, que vence en 2 minutos.</summary>
public sealed class AuthorizationGrants : IAuthorizationGrants
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, Grant> _grants = new();
    private readonly IClock _clock;

    public AuthorizationGrants(IClock clock) => _clock = clock;

    public Guid Issue(Permission permission, Guid requestedBy, Guid authorizedBy)
    {
        var now = _clock.UtcNow;
        foreach (var expired in _grants.Where(g => g.Value.ExpiresAt <= now).Select(g => g.Key))
        {
            _grants.TryRemove(expired, out _);
        }

        var id = Guid.CreateVersion7();
        _grants[id] = new Grant(permission, requestedBy, authorizedBy, now + Lifetime);
        return id;
    }

    public Guid? TryConsume(Guid grantId, Permission permission, Guid requestedBy)
    {
        if (!_grants.TryGetValue(grantId, out var grant))
        {
            return null;
        }

        if (grant.Permission != permission || grant.RequestedBy != requestedBy)
        {
            return null;
        }

        // Solo uno de los consumidores concurrentes se la queda.
        if (!_grants.TryRemove(grantId, out _))
        {
            return null;
        }

        return grant.ExpiresAt > _clock.UtcNow ? grant.AuthorizedBy : null;
    }

    private sealed record Grant(Permission Permission, Guid RequestedBy, Guid AuthorizedBy, DateTime ExpiresAt);
}
