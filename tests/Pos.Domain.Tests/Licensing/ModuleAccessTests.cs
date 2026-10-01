using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Domain.Tests.Licensing;

/// <summary>012, FR-021: qué permisos pertenecen a un módulo y cuáles nunca se bloquean.</summary>
public sealed class ModuleAccessTests
{
    [Theory]
    [InlineData(Permission.ViewInventory, LicensedModule.Inventory)]
    [InlineData(Permission.RegisterMovements, LicensedModule.Inventory)]
    [InlineData(Permission.ViewReports, LicensedModule.AdvancedReports)]
    [InlineData(Permission.OperateShift, LicensedModule.CashShifts)]
    [InlineData(Permission.WithdrawCash, LicensedModule.CashShifts)]
    [InlineData(Permission.ManageShifts, LicensedModule.CashShifts)]
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
    public void PermisosDeModulo_MapeanASuModulo(Permission permission, LicensedModule expected) =>
        Assert.Equal(expected, ModuleAccess.Required(permission));

    [Theory]
    [InlineData(Permission.Sell)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.ManageLicense)]
    [InlineData(Permission.CancelSales)]
    public void FuncionesBasicas_NuncaSeBloquean(Permission permission) =>
        Assert.Null(ModuleAccess.Required(permission));
}
