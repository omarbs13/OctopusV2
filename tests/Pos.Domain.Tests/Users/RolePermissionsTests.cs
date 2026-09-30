using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class RolePermissionsTests
{
    [Fact]
    public void Cajero_TieneExactamenteLosPermisosDeLaHistoria4()
    {
        Assert.Equal(
            [Permission.Sell, Permission.ViewOwnSales, Permission.ViewProducts, Permission.ViewInventory],
            RolePermissions.For(UserRole.Cashier).Order());
    }

    [Fact]
    public void Administrador_TieneTodosLosPermisos()
    {
        Assert.All(Enum.GetValues<Permission>(), p => Assert.True(RolePermissions.Has(UserRole.Admin, p)));
    }

    [Fact]
    public void SoloCancelarVentaYAbrirCajonSeAutorizan()
    {
        var authorizable = Enum.GetValues<Permission>().Where(RolePermissions.IsAuthorizable);

        Assert.Equal([Permission.CancelSales, Permission.OpenDrawerWithoutSale], authorizable.Order());
    }
}
