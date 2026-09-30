using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Users.ListCashiers;

/// <summary>Usuarios con ventas o activos para el filtro de "Ventas realizadas"; sin "Sistema".</summary>
public sealed class ListCashiersHandler
{
    private readonly IAccessControl _access;
    private readonly IUserRepository _users;

    public ListCashiersHandler(IAccessControl access, IUserRepository users)
    {
        _access = access;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<UserOption>>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewAllSales, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<UserOption>>(access.Error!);
        }

        return Result.Success(await _users.ListCashiersAsync(cancellationToken));
    }
}
