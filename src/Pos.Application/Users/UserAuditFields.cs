using Pos.Application.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>
/// Instantánea de auditoría del usuario (018, research §6). <b>Nunca</b> incluye la contraseña ni su
/// hash (FR-006).
/// </summary>
public static class UserAuditFields
{
    public const string State = "Estado";

    public static IReadOnlyList<AuditField> Snapshot(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return
        [
            new("Nombre completo", user.FullName),
            new("Usuario", user.UserName),
            new("Rol", RoleName(user.Role)),
            new(State, AuditFormat.ActiveState(user.IsActive)),
        ];
    }

    private static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Cashier => "Cajero",
        _ => role.ToString(),
    };
}
