using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Domain.Tests.Licensing;

/// <summary>012, FR-021; 025, data-model: qué módulos exige cada permiso y cuáles nunca se bloquean por módulo.</summary>
public sealed class ModuleAccessTests
{
    [Theory]
    [InlineData(Permission.ViewInventory, LicensedModule.Inventory)]
    [InlineData(Permission.RegisterMovements, LicensedModule.Inventory)]
    [InlineData(Permission.ViewReports, LicensedModule.AdvancedReports)]
    [InlineData(Permission.OperateShift, LicensedModule.CashShifts)]
    [InlineData(Permission.WithdrawCash, LicensedModule.CashShifts)]
    [InlineData(Permission.ManageShifts, LicensedModule.CashShifts)]
    [InlineData(Permission.GenerateShiftReadout, LicensedModule.CashShifts)]
    [InlineData(Permission.ProcessReturns, LicensedModule.Returns)]
    [InlineData(Permission.ApproveReturns, LicensedModule.Returns)]
    [InlineData(Permission.ManageCreditNotes, LicensedModule.Returns)]
    [InlineData(Permission.ManageCustomers, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.SellOnCredit, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.RegisterCustomerPayments, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.ManageCustomerCredit, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.ApproveCreditOverLimit, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.VoidCustomerPayments, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.ViewReceivables, LicensedModule.CreditAndCustomers)]
    [InlineData(Permission.ApplyDiscounts, LicensedModule.Discounts)]
    [InlineData(Permission.ApproveDiscounts, LicensedModule.Discounts)]
    [InlineData(Permission.ManageDiscounts, LicensedModule.Discounts)]
    [InlineData(Permission.ViewDiscountReport, LicensedModule.Discounts)]
    [InlineData(Permission.ManageSuppliers, LicensedModule.Suppliers)]
    [InlineData(Permission.ViewPurchaseReport, LicensedModule.Suppliers)]
    [InlineData(Permission.ManageCategories, LicensedModule.Categories)]
    public void PermisosDeUnModulo_MapeanASuModulo(Permission permission, LicensedModule expected) =>
        Assert.Equal([expected], ModuleAccess.RequiredModules(permission));

    [Theory]
    [InlineData(Permission.RegisterPurchases)]
    [InlineData(Permission.VoidPurchases)]
    public void ComprasAProveedores_ExigenProveedoresEInventario(Permission permission) =>
        Assert.Equal([LicensedModule.Suppliers, LicensedModule.Inventory], ModuleAccess.RequiredModules(permission));

    [Theory]
    [InlineData(Permission.Sell)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.ManageLicense)]
    [InlineData(Permission.ExportBackup)]
    [InlineData(Permission.CancelSales)]
    [InlineData(Permission.ManageProducts)]
    public void FuncionesBasicas_NoExigenModulo(Permission permission) =>
        Assert.Empty(ModuleAccess.RequiredModules(permission));

    [Fact]
    public void PrimerModuloInactivo_SigueElOrdenDeLaTabla()
    {
        Assert.Equal(LicensedModule.Suppliers, ModuleAccess.FirstInactive(Permission.RegisterPurchases, _ => false));
        Assert.Equal(LicensedModule.Inventory, ModuleAccess.FirstInactive(Permission.RegisterPurchases, m => m == LicensedModule.Suppliers));
        Assert.Null(ModuleAccess.FirstInactive(Permission.RegisterPurchases, _ => true));
    }
}
