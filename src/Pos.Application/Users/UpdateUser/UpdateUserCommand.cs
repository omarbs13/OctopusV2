using Pos.Domain.Users;

namespace Pos.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid Id,
    int ExpectedVersion,
    string FullName,
    string UserName,
    UserRole Role,
    bool IsActive);
