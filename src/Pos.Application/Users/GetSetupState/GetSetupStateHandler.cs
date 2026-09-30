using Pos.Application.Abstractions;

namespace Pos.Application.Users.GetSetupState;

/// <summary>Indica si falta crear el primer administrador: no existe ningún usuario real (FR-001).</summary>
public sealed class GetSetupStateHandler
{
    private readonly IUserRepository _users;

    public GetSetupStateHandler(IUserRepository users) => _users = users;

    public async Task<Result<SetupState>> HandleAsync(CancellationToken cancellationToken) =>
        Result.Success(new SetupState(!await _users.AnyRealUserAsync(cancellationToken)));
}
