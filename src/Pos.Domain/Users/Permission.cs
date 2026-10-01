using System.Diagnostics.CodeAnalysis;

namespace Pos.Domain.Users;

/// <summary>Capacidad puntual que un rol puede tener (FR-010). La asignación vive en <see cref="RolePermissions"/>.</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "El nombre del dominio es Permiso (Permission).")]
public enum Permission
{
    Sell,
    ViewOwnSales,
    ViewAllSales,
    ViewProducts,
    ManageProducts,
    ViewInventory,
    RegisterMovements,
    CancelSales,
    OpenDrawerWithoutSale,
    ManageUsers,
    ViewAuditLog,
    ManageSettings,
    ExportDiagnostics,
    OperateShift,
    WithdrawCash,
    ManageShifts,
    ViewReports,
}
