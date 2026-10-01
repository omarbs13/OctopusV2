using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class RolePermissionsTests
{
    [Fact]
    public void Cajero_TieneExactamenteSusPermisos()
    {
        Assert.Equal(
            [
                Permission.Sell, Permission.ViewOwnSales, Permission.ViewProducts, Permission.ViewInventory, Permission.OperateShift,
                Permission.ProcessReturns, Permission.ManageCustomers, Permission.SellOnCredit, Permission.RegisterCustomerPayments,
            ],
            RolePermissions.For(UserRole.Cashier).Order());
    }

    [Fact]
    public void Administrador_TieneTodosLosPermisos()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(RolePermissions.Has(UserRole.Admin, p)));
    }

    [Fact]
    public void SoloCancelarVentaCajonRetiroAprobarDevolucionesExcederLimiteYAnularAbonosSeAutorizan()
    {
        var authorizable = Enum.GetValues<Permission>().Where(RolePermissions.IsAuthorizable);

        Assert.Equal(
            [
                Permission.CancelSales, Permission.OpenDrawerWithoutSale, Permission.WithdrawCash, Permission.ApproveReturns,
                Permission.ApproveCreditOverLimit, Permission.VoidCustomerPayments,
            ],
            authorizable.Order());
    }
}
