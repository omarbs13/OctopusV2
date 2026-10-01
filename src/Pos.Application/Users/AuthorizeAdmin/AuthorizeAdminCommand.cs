using Pos.Domain.Users;

namespace Pos.Application.Users.AuthorizeAdmin;

/// <summary>
/// <c>Context</c> (015, FR-019) describe la operación que se autoriza, por ejemplo el descuento y su monto;
/// se agrega al detalle de la bitácora. Nunca incluye la contraseña.
/// </summary>
public sealed record AuthorizeAdminCommand(Permission Permission, string UserName, string Password, string? Context = null)
{
    /// <summary>La contraseña nunca sale en <c>ToString</c> (logs, excepciones; SC-008).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Permission = ").Append(Permission).Append(", UserName = ").Append(UserName).Append(", Context = ").Append(Context);
        return true;
    }
}
