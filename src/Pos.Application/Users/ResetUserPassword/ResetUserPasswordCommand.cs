namespace Pos.Application.Users.ResetUserPassword;

public sealed record ResetUserPasswordCommand(Guid UserId, string Password, string ConfirmPassword)
{
    /// <summary>Las contraseñas nunca salen en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("UserId = ").Append(UserId);
        return true;
    }
}
