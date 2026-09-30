using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Control de acceso que permite todo; para las pruebas que no verifican permisos.</summary>
public sealed class AllowAllAccessControl : IAccessControl
{
    public Task<AccessDecision> CheckAsync(Permission permission, CancellationToken cancellationToken) =>
        Task.FromResult(AccessDecision.Allow());

    public Task<AccessDecision> CheckAsync(Permission permission, Guid? authorizationGrantId, CancellationToken cancellationToken) =>
        Task.FromResult(AccessDecision.Allow());

    public Task<bool> HasAsync(Permission permission, CancellationToken cancellationToken) => Task.FromResult(true);
}
