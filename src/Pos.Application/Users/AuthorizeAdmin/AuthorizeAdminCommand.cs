using Pos.Domain.Users;

namespace Pos.Application.Users.AuthorizeAdmin;

public sealed record AuthorizeAdminCommand(Permission Permission, string UserName, string Password)
{
    /// <summary>La contraseña nunca sale en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Permission = ").Append(Permission).Append(", UserName = ").Append(UserName);
        return true;
    }
}
