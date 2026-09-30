using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Users.SearchUsers;

/// <summary>Listado de usuarios para el Administrador; nunca incluye a "Sistema" (FR-015).</summary>
public sealed class SearchUsersHandler
{
    private readonly IAccessControl _access;
    private readonly IUserRepository _users;

    public SearchUsersHandler(IAccessControl access, IUserRepository users)
    {
        _access = access;
        _users = users;
    }

    public async Task<Result<UserPage>> HandleAsync(SearchUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageUsers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<UserPage>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        var search = new UserSearch(text, query.IncludeInactive, Math.Max(query.Page, 1), UserPage.DefaultPageSize);
        return Result.Success(await _users.SearchAsync(search, cancellationToken));
    }
}
