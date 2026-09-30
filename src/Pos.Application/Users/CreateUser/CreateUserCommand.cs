using Pos.Domain.Users;

namespace Pos.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string FullName,
    string UserName,
    UserRole Role,
    bool IsActive,
    string Password,
    string ConfirmPassword)
{
    /// <summary>Las contraseñas nunca salen en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName);
        return true;
    }
}
