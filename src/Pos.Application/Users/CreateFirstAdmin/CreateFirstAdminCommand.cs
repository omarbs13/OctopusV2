namespace Pos.Application.Users.CreateFirstAdmin;

public sealed record CreateFirstAdminCommand(string FullName, string UserName, string Password, string ConfirmPassword)
{
    /// <summary>Las contraseñas nunca salen en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName);
        return true;
    }
}
