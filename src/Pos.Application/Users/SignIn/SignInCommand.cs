namespace Pos.Application.Users.SignIn;

public sealed record SignInCommand(string UserName, string Password)
{
    /// <summary>La contraseña nunca sale en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName);
        return true;
    }
}
