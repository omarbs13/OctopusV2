namespace Pos.Application.Users.SearchUsers;

public sealed record SearchUsersQuery(string? Text, bool IncludeInactive, int Page = 1);
