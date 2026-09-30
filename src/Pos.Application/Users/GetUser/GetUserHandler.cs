using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Users.GetUser;

public sealed class GetUserHandler
{
    private readonly IAccessControl _access;
    private readonly IUserRepository _users;
    private readonly ISaleDraftStore _drafts;

    public GetUserHandler(IAccessControl access, IUserRepository users, ISaleDraftStore drafts)
    {
        _access = access;
        _users = users;
        _drafts = drafts;
    }

    public async Task<Result<UserDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageUsers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<UserDto>(access.Error!);
        }

        var user = await _users.GetAsync(id, cancellationToken);
        return user is null || user.IsSystem
            ? Result.Failure<UserDto>(new NotFound())
            : Result.Success(new UserDto(
                user.Id,
                user.FullName,
                user.UserName,
                user.Role,
                user.IsActive,
                user.Version,
                await _drafts.HasForAsync(user.Id, cancellationToken)));
    }
}
