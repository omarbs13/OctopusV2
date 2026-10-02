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
                Permission.ApplyDiscounts,
            ],
            RolePermissions.For(UserRole.Cashier).Order());
    }

    [Fact]
    public void Administrador_TieneTodosLosPermisos()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(RolePermissions.Has(UserRole.Admin, p)));
    }

    [Fact]
    public void SoloCancelarVentaCajonRetiroAprobarDevolucionesExcederLimiteAnularAbonosAprobarDescuentosYCorteXSeAutorizan()
    {
        var authorizable = Enum.GetValues<Permission>().Where(RolePermissions.IsAuthorizable);

        Assert.Equal(
            [
                Permission.CancelSales, Permission.OpenDrawerWithoutSale, Permission.WithdrawCash, Permission.ApproveReturns,
                Permission.ApproveCreditOverLimit, Permission.VoidCustomerPayments, Permission.ApproveDiscounts,
                Permission.GenerateShiftReadout,
            ],
            authorizable.Order());
    }

    [Theory]
    [InlineData(Permission.ManageSuppliers)]
    [InlineData(Permission.RegisterPurchases)]
    [InlineData(Permission.VoidPurchases)]
    [InlineData(Permission.ViewPurchaseReport)]
    public void PermisosDeCompras_SoloDelAdministradorYNoAutorizables(Permission permission)
    {
        Assert.False(RolePermissions.Has(UserRole.Cashier, permission));
        Assert.True(RolePermissions.Has(UserRole.Admin, permission));
        Assert.False(RolePermissions.IsAuthorizable(permission));
    }

    [Fact]
    public void RegistrarComprasYRegistrarMovimientos_SonPermisosIndependientes()
    {
        Assert.NotEqual(Permission.RegisterPurchases, Permission.RegisterMovements);
        Assert.False(RolePermissions.Has(UserRole.Cashier, Permission.RegisterPurchases));
        Assert.False(RolePermissions.Has(UserRole.Cashier, Permission.RegisterMovements));
    }
}
