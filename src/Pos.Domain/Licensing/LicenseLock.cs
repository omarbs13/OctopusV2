using System.Collections.Frozen;
using Pos.Domain.Users;

namespace Pos.Domain.Licensing;

/// <summary>
/// Permisos que siguen disponibles con el sistema bloqueado (025, FR-028, research §10): licencia,
/// respaldo y los necesarios para terminar la venta en curso y cerrar el turno abierto (Principio I).
/// La regla de módulo sigue aplicando encima.
/// </summary>
public static class LicenseLock
{
    public static IReadOnlySet<Permission> ExemptPermissions { get; } = new[]
    {
        Permission.ManageLicense,
        Permission.ExportBackup,
        Permission.Sell,
        Permission.ApplyDiscounts,
        Permission.ApproveDiscounts,
        Permission.SellOnCredit,
        Permission.ApproveCreditOverLimit,
        Permission.OperateShift,
        Permission.ManageShifts,
    }.ToFrozenSet();

    public static bool IsExempt(Permission permission) => ExemptPermissions.Contains(permission);
}
