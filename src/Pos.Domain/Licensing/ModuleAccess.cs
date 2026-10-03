using Pos.Domain.Users;

namespace Pos.Domain.Licensing;

/// <summary>Módulos que exige cada permiso (012, research §2; 025, data-model); vacío = función siempre disponible.</summary>
public static class ModuleAccess
{
    private static readonly LicensedModule[] None = [];
    private static readonly LicensedModule[] Inventory = [LicensedModule.Inventory];
    private static readonly LicensedModule[] Suppliers = [LicensedModule.Suppliers];
    private static readonly LicensedModule[] Purchases = [LicensedModule.Suppliers, LicensedModule.Inventory];
    private static readonly LicensedModule[] Reports = [LicensedModule.AdvancedReports];
    private static readonly LicensedModule[] CashShifts = [LicensedModule.CashShifts];
    private static readonly LicensedModule[] Returns = [LicensedModule.Returns];
    private static readonly LicensedModule[] Credit = [LicensedModule.CreditAndCustomers];
    private static readonly LicensedModule[] Discounts = [LicensedModule.Discounts];
    private static readonly LicensedModule[] Categories = [LicensedModule.Categories];

    public static IReadOnlyList<LicensedModule> RequiredModules(Permission permission) => permission switch
    {
        Permission.ViewInventory or Permission.RegisterMovements => Inventory,
        Permission.ManageSuppliers or Permission.ViewPurchaseReport => Suppliers,

        // Una compra a proveedor mueve existencias: exige también Inventario (FR-007).
        Permission.RegisterPurchases or Permission.VoidPurchases => Purchases,
        Permission.ViewReports => Reports,
        Permission.OperateShift or Permission.WithdrawCash or Permission.ManageShifts
            or Permission.GenerateShiftReadout => CashShifts,
        Permission.ProcessReturns or Permission.ApproveReturns or Permission.ManageCreditNotes => Returns,
        Permission.ManageCustomers or Permission.SellOnCredit or Permission.RegisterCustomerPayments
            or Permission.ManageCustomerCredit or Permission.ApproveCreditOverLimit or Permission.VoidCustomerPayments
            or Permission.ViewReceivables => Credit,
        Permission.ApplyDiscounts or Permission.ApproveDiscounts or Permission.ManageDiscounts
            or Permission.ViewDiscountReport => Discounts,
        Permission.ManageCategories => Categories,
        _ => None,
    };

    /// <summary>Primer módulo requerido que no está activo; nulo si todos lo están.</summary>
    public static LicensedModule? FirstInactive(Permission permission, Func<LicensedModule, bool> isActive)
    {
        ArgumentNullException.ThrowIfNull(isActive);
        foreach (var module in RequiredModules(permission))
        {
            if (!isActive(module))
            {
                return module;
            }
        }

        return null;
    }
}
