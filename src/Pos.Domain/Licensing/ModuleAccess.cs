using Pos.Domain.Users;

namespace Pos.Domain.Licensing;

/// <summary>Módulo al que pertenece cada permiso (012, research §2); <c>null</c> = función siempre disponible.</summary>
public static class ModuleAccess
{
    public static LicensedModule? Required(Permission permission) => permission switch
    {
        Permission.ViewInventory or Permission.RegisterMovements => LicensedModule.Inventory,
        Permission.ViewReports => LicensedModule.AdvancedReports,
        Permission.OperateShift or Permission.WithdrawCash or Permission.ManageShifts => LicensedModule.CashShifts,
        Permission.ProcessReturns or Permission.ApproveReturns or Permission.ManageCreditNotes => LicensedModule.Returns,
        Permission.ManageCustomers or Permission.SellOnCredit or Permission.RegisterCustomerPayments
            or Permission.ManageCustomerCredit or Permission.ApproveCreditOverLimit or Permission.VoidCustomerPayments
            or Permission.ViewReceivables => LicensedModule.CreditAndCustomers,
        _ => null,
    };
}
