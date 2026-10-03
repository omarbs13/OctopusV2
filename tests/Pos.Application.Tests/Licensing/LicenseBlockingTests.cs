using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>
/// 025, H1/H6 (FR-007, FR-027 a FR-030a): regla de módulo con varios módulos por permiso, bloqueo total sin POS
/// en el control de acceso y "terminar trabajo ya iniciado" sin las reglas de bloqueo ni de módulo.
/// </summary>
public sealed class LicenseBlockingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AccessControl Build(UserRole role, params LicensedModule[] licensed)
    {
        var auth = new AuthFixture();
        var state = new LicenseState(auth.Clock);
        var since = DateOnly.FromDateTime(auth.Clock.UtcNow).AddYears(-1);
        var firstRun = auth.Clock.UtcNow.AddDays(-60);
        state.Set(
            new TrialRecord(FakeMachine.Id, firstRun, firstRun, 30, firstRun.AddDays(1)),
            new SignedLicense(Guid.CreateVersion7(), firstRun, FakeMachine.Id, "C", [.. licensed.Select(m => new ModuleGrant(m, since, null))]),
            false);
        auth.SignedIn(auth.AddUser("usuario", role));
        return new AccessControl(auth.Session, auth.Users, auth.Grants, NullLogger<AccessControl>.Instance, state);
    }

    private static AccessControl Licensed(params LicensedModule[] modules) => Build(UserRole.Admin, [LicensedModule.Pos, .. modules]);

    private static AccessControl Blocked(params LicensedModule[] modules) => Build(UserRole.Admin, modules);

    // Proveedores y Categorías por licencia (H1)

    [Theory]
    [InlineData(Permission.ManageSuppliers)]
    [InlineData(Permission.RegisterPurchases)]
    public async Task SinProveedores_SeRechazaConModuloNoActivo(Permission permission)
    {
        var decision = await Licensed(LicensedModule.Inventory).CheckAsync(permission, Ct);

        Assert.Equal(LicensedModule.Suppliers, Assert.IsType<ModuleNotLicensed>(decision.Error).Module);
    }

    [Fact]
    public async Task CompraConProveedoresPeroSinInventario_ExigeInventario()
    {
        var decision = await Licensed(LicensedModule.Suppliers).CheckAsync(Permission.RegisterPurchases, Ct);

        Assert.Equal(LicensedModule.Inventory, Assert.IsType<ModuleNotLicensed>(decision.Error).Module);
    }

    [Fact]
    public async Task SinCategorias_SeRechazaConModuloNoActivo()
    {
        var access = Licensed();

        Assert.Equal(LicensedModule.Categories, Assert.IsType<ModuleNotLicensed>((await access.CheckAsync(Permission.ManageCategories, Ct)).Error).Module);
        Assert.True((await Licensed(LicensedModule.Categories).CheckAsync(Permission.ManageCategories, Ct)).Allowed);
    }

    [Theory]
    [InlineData(Permission.Sell)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.ViewProducts)]
    public async Task ConPosActivo_LasFuncionesBasicasSiguenDisponibles(Permission permission) =>
        Assert.True((await Licensed().CheckAsync(permission, Ct)).Allowed);

    // Bloqueo total (H6)

    [Theory]
    [InlineData(Permission.ViewProducts)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.CancelSales)]
    [InlineData(Permission.ViewInventory)]
    [InlineData(Permission.OpenDrawerWithoutSale)]
    public async Task EnBloqueo_TodoPermisoNoExento_SeRechazaConSistemaNoActivado(Permission permission)
    {
        var access = Blocked(LicensedModule.Inventory);

        var decision = await access.CheckAsync(permission, Ct);

        Assert.Equal(LicenseBlockReason.BaseNotLicensed, Assert.IsType<SystemNotActivated>(decision.Error).Reason);
        Assert.False(await access.HasAsync(permission, Ct));
    }

    [Theory]
    [InlineData(Permission.ManageLicense)]
    [InlineData(Permission.ExportBackup)]
    [InlineData(Permission.Sell)]
    public async Task EnBloqueo_LosPermisosExentos_SiguenLaReglaDeRol(Permission permission)
    {
        Assert.True((await Blocked().CheckAsync(permission, Ct)).Allowed);
        Assert.IsType<Forbidden>((await Build(UserRole.Cashier).CheckAsync(Permission.ExportBackup, Ct)).Error);
    }

    [Fact]
    public async Task EnBloqueo_UnPermisoExento_SigueExigiendoSuModulo()
    {
        var decision = await Blocked().CheckAsync(Permission.SellOnCredit, Ct);

        Assert.Equal(LicensedModule.CreditAndCustomers, Assert.IsType<ModuleNotLicensed>(decision.Error).Module);
        Assert.True((await Blocked(LicensedModule.CreditAndCustomers).CheckAsync(Permission.SellOnCredit, Ct)).Allowed);
    }

    [Theory]
    [InlineData(Permission.ApplyDiscounts, LicensedModule.Discounts)]
    [InlineData(Permission.ApproveDiscounts, LicensedModule.Discounts)]
    [InlineData(Permission.SellOnCredit, LicensedModule.CreditAndCustomers)]
    public async Task EnBloqueo_ApoyosDeLaVentaEnCurso_SiguenLaReglaDeModulo(Permission permission, LicensedModule module)
    {
        // ResolveCoupon, ApproveDiscount, GetDiscountSettings, FindCustomersForSale y GetCustomerCreditStatus (blocked-mode §1).
        Assert.True((await Blocked(module).CheckAsync(permission, Ct)).Allowed);
        Assert.Equal(module, Assert.IsType<ModuleNotLicensed>((await Blocked().CheckAsync(permission, Ct)).Error).Module);
    }

    [Fact]
    public async Task TerminarTrabajoIniciado_OmiteBloqueoYModulo_PeroExigeSesionYRol()
    {
        Assert.True((await Blocked().CheckToFinishAsync(Permission.OperateShift, Ct)).Allowed);
        Assert.True((await Blocked().CheckToFinishAsync(Permission.ApplyDiscounts, Ct)).Allowed);
        Assert.IsType<Forbidden>((await Build(UserRole.Cashier).CheckToFinishAsync(Permission.ManageShifts, Ct)).Error);

        var auth = new AuthFixture();
        var noSession = new AccessControl(auth.Session, auth.Users, auth.Grants, NullLogger<AccessControl>.Instance);
        Assert.IsType<Forbidden>((await noSession.CheckToFinishAsync(Permission.Sell, Ct)).Error);
    }

    [Fact]
    public async Task SoloSesion_PermiteACualquierUsuario_TambienEnBloqueo()
    {
        Assert.True((await Build(UserRole.Cashier).CheckSessionAsync(Ct)).Allowed);

        var auth = new AuthFixture();
        var noSession = new AccessControl(auth.Session, auth.Users, auth.Grants, NullLogger<AccessControl>.Instance);
        Assert.IsType<SessionRequired>((await noSession.CheckSessionAsync(Ct)).Error);
    }
}
