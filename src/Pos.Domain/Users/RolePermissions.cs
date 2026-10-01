using System.Collections.Frozen;

namespace Pos.Domain.Users;

/// <summary>Único punto donde se asignan permisos a roles (FR-012).</summary>
public static class RolePermissions
{
    private static readonly FrozenSet<Permission> AdminPermissions =
        Enum.GetValues<Permission>().ToFrozenSet();

    private static readonly FrozenSet<Permission> CashierPermissions = new[]
    {
        Permission.Sell,
        Permission.ViewOwnSales,
        Permission.ViewProducts,
        Permission.ViewInventory,
        Permission.OperateShift,
        Permission.ProcessReturns,
    }.ToFrozenSet();

    private static readonly FrozenSet<Permission> Authorizable = new[]
    {
        Permission.CancelSales,
        Permission.OpenDrawerWithoutSale,
        Permission.WithdrawCash,
        Permission.ApproveReturns,
    }.ToFrozenSet();

    public static IReadOnlySet<Permission> For(UserRole role) => role switch
    {
        UserRole.Admin => AdminPermissions,
        UserRole.Cashier => CashierPermissions,
        _ => FrozenSet<Permission>.Empty,
    };

    public static bool Has(UserRole role, Permission permission) => For(role).Contains(permission);

    /// <summary>Indica si un administrador puede autorizar la operación a un usuario sin el permiso (FR-013).</summary>
    public static bool IsAuthorizable(Permission permission) => Authorizable.Contains(permission);
}
