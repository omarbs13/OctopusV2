namespace Pos.Application.Users.ChangeOwnPassword;

/// <summary>
/// Cambio de la propia contraseña. Con <c>SealedUserId</c> es el cambio obligatorio tras el inicio
/// de sesión (aún sin sesión): no pide la contraseña actual porque acaba de verificarse.
/// </summary>
public sealed record ChangeOwnPasswordCommand(
    string? CurrentPassword,
    string NewPassword,
    string ConfirmPassword,
    Guid? SealedUserId = null)
{
    /// <summary>Las contraseñas nunca salen en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Mandatory = ").Append(SealedUserId is not null);
        return true;
    }
}
