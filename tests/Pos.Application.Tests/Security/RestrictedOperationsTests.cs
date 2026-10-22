using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit.SearchAuditLog;
using Pos.Application.Business.SaveBusinessProfile;
using Pos.Application.CashShifts.GetShiftDetail;
using Pos.Application.CashShifts.RegisterCashMovement;
using Pos.Application.CashShifts.SearchShifts;
using Pos.Application.Diagnostics.ExportDiagnostics;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Printing;
using Pos.Application.Printing.ListPrinters;
using Pos.Application.Printing.OpenCashDrawer;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Printing.SavePrintingSettings;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Products.PrepareProductImage;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.GetSalesDashboard;
using Pos.Application.Security;
using Pos.Application.Security.SaveSecuritySettings;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.CreateUser;
using Pos.Application.Users.GetUser;
using Pos.Application.Users.ListCashiers;
using Pos.Application.Users.ResetUserPassword;
using Pos.Application.Users.SearchUsers;
using Pos.Application.Users.UpdateUser;
using Pos.Domain.CashShifts;
using Pos.Domain.Inventory;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Security;

/// <summary>
/// SC-002: toda operación restringida rechaza al Cajero con <see cref="Forbidden"/> al invocarse
/// directamente. Los handlers se arman con dependencias nulas: como el permiso se verifica antes de
/// tocar cualquiera, un acceso indebido habría lanzado en lugar de devolver <see cref="Forbidden"/>.
/// </summary>
public class RestrictedOperationsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, Permission, Func<AuthFixture, Task<Result>>> Restricted() => new()
    {
        { "CreateProduct", Permission.ManageProducts, f => R(new CreateProductHandler(f.Access, null!, null!, null!, null!, null!, null!).HandleAsync(new CreateProductCommand("a", "b", null, "1", "H87"), Ct)) },
        { "UpdateProduct", Permission.ManageProducts, f => R(new UpdateProductHandler(f.Access, null!, null!, null!, null!, null!, null!, null!).HandleAsync(new UpdateProductCommand(Guid.NewGuid(), 1, "a", "b", null, "1", "H87", true), Ct)) },
        { "DeleteProduct", Permission.ManageProducts, f => R(new DeleteProductHandler(f.Access, null!, null!, null!, null!).HandleAsync(new DeleteProductCommand(Guid.NewGuid(), 1), Ct)) },
        { "PrepareProductImage", Permission.ManageProducts, f => R(new PrepareProductImageHandler(f.Access, null!).HandleAsync(new PrepareProductImageCommand(Stream.Null, 0), Ct)) },
        { "RegisterMovement", Permission.RegisterMovements, f => R(new RegisterMovementHandler(f.Access, f.Session, null!, null!, null!, null!, NullLogger<RegisterMovementHandler>.Instance).HandleAsync(new RegisterMovementCommand(Guid.NewGuid(), MovementType.Receipt, "1", null, null), Ct)) },
        { "CancelSale", Permission.CancelSales, f => R(new CancelSaleHandler(f.Access, null!, null!, null!, null!, null!, null!, null!, null!, NullLogger<CancelSaleHandler>.Instance).HandleAsync(new CancelSaleCommand(Guid.NewGuid(), 1, "motivo"), Ct)) },
        { "OpenCashDrawerManual", Permission.OpenDrawerWithoutSale, f => R(new OpenCashDrawerHandler(f.Access, null!, null!, null!, null!, NullLogger<OpenCashDrawerHandler>.Instance).HandleAsync(OpenCashDrawerCommand.Manual("motivo"), Ct)) },
        { "GetSalesDashboard", Permission.ViewAllSales, f => R(new GetSalesDashboardHandler(f.Access, null!).HandleAsync(new SalesDashboardQuery([]), Ct)) },
        { "SaveBusinessProfile", Permission.ManageSettings, f => R(new SaveBusinessProfileHandler(f.Access, null!, null!, null!).HandleAsync(new SaveBusinessProfileCommand("a", "b", "c", null, null, LogoChange.KeepCurrent), Ct)) },
        { "SavePrintingSettings", Permission.ManageSettings, f => R(new SavePrintingSettingsHandler(f.Access, null!).HandleAsync(new PrintingSettings(), Ct)) },
        { "ListPrinters", Permission.ManageSettings, f => R(new ListPrintersHandler(f.Access, null!, NullLogger<ListPrintersHandler>.Instance).HandleAsync(Ct)) },
        { "ExportDiagnostics", Permission.ExportDiagnostics, f => R(new ExportDiagnosticsHandler(f.Access, null!).HandleAsync(new ExportDiagnosticsCommand("/tmp/x.zip"), Ct)) },
        { "PrintTestTicket", Permission.ManageSettings, f => R(new PrintTicketHandler(f.Access, f.Session, null!, null!, null!, null!, null!, NullLogger<PrintTicketHandler>.Instance).HandleAsync(new PrintTicketCommand(PrintSource.Sample), Ct)) },
        { "SearchUsers", Permission.ManageUsers, f => R(new SearchUsersHandler(f.Access, null!).HandleAsync(new SearchUsersQuery(null, false), Ct)) },
        { "GetUser", Permission.ManageUsers, f => R(new GetUserHandler(f.Access, null!, null!).HandleAsync(Guid.NewGuid(), Ct)) },
        { "CreateUser", Permission.ManageUsers, f => R(new CreateUserHandler(f.Access, null!, null!, null!, null!, null!, NullLogger<CreateUserHandler>.Instance).HandleAsync(new CreateUserCommand("a", "abc", UserRole.Cashier, true, "12345678", "12345678"), Ct)) },
        { "UpdateUser", Permission.ManageUsers, f => R(new UpdateUserHandler(f.Access, f.Session, null!, null!, null!, null!, null!, NullLogger<UpdateUserHandler>.Instance).HandleAsync(new UpdateUserCommand(Guid.NewGuid(), 1, "a", "abc", UserRole.Cashier, true), Ct)) },
        { "ResetUserPassword", Permission.ManageUsers, f => R(new ResetUserPasswordHandler(f.Access, f.Session, null!, null!, null!, null!, null!, NullLogger<ResetUserPasswordHandler>.Instance).HandleAsync(new ResetUserPasswordCommand(Guid.NewGuid(), "12345678", "12345678"), Ct)) },
        { "ListCashiers", Permission.ViewAllSales, f => R(new ListCashiersHandler(f.Access, null!).HandleAsync(Ct)) },
        { "SearchAuditLog", Permission.ViewAuditLog, f => R(new SearchAuditLogHandler(f.Access, null!, null!).HandleAsync(new SearchAuditLogQuery(null, null, null, null), Ct)) },
        { "RegisterWithdrawal", Permission.WithdrawCash, f => R(new RegisterCashMovementHandler(f.Access, null!, null!, f.Session, null!, null!, new RegisterCashMovementValidator(), NullLogger<RegisterCashMovementHandler>.Instance).HandleAsync(new RegisterCashMovementCommand(Guid.NewGuid(), CashMovementType.Out, 100, "motivo"), Ct)) },
        { "SearchShifts", Permission.ManageShifts, f => R(new SearchShiftsHandler(f.Access, null!).HandleAsync(new SearchShiftsQuery(null, null, null, null), Ct)) },
        { "GetShiftDetail", Permission.ManageShifts, f => R(new GetShiftDetailHandler(f.Access, null!, null!).HandleAsync(new GetShiftDetailQuery(Guid.NewGuid()), Ct)) },
        { "SaveSecuritySettings", Permission.ManageSettings, f => R(new SaveSecuritySettingsHandler(f.Access, null!).HandleAsync(new SecuritySettings(), Ct)) },
    };

    private static async Task<Result> R(Task<Result> task) => await task;

    private static async Task<Result> R<T>(Task<Result<T>> task) => await task;

    [Theory]
    [MemberData(nameof(Restricted))]
#pragma warning disable xUnit1026 // El nombre identifica el caso en el informe.
    public async Task UnCajero_ElHandlerRestringidoDevuelveForbiddenSinEfectos(string name, Permission permission, Func<AuthFixture, Task<Result>> invoke)
#pragma warning restore xUnit1026
    {
        var auth = new AuthFixture();
        auth.SignedIn(auth.AddUser("caja", UserRole.Cashier));

        var result = await invoke(auth);

        var forbidden = Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(permission, forbidden.Permission);
        Assert.Equal(RolePermissions.IsAuthorizable(permission), forbidden.CanBeAuthorized);
        Assert.Equal(name, name);
    }

    [Fact]
    public async Task UnUsuarioDesactivadoConLaSesionAbierta_TambienEsRechazado()
    {
        var auth = new AuthFixture();
        var admin = auth.AddUser("admin", UserRole.Admin);
        auth.SignedIn(admin);
        admin.Deactivate();

        var decision = await auth.Access.CheckAsync(Permission.ManageUsers, Ct);

        Assert.False(decision.Allowed);
        Assert.False(Assert.IsType<Forbidden>(decision.Error).CanBeAuthorized);
    }

    [Fact]
    public async Task SinSesion_TodoEsRechazado()
    {
        var auth = new AuthFixture();

        Assert.False((await auth.Access.CheckAsync(Permission.Sell, Ct)).Allowed);
    }

    [Fact]
    public async Task ElAdministrador_TieneTodosLosPermisosYElHandlerSeEjecuta()
    {
        var auth = new AuthFixture();
        auth.SignedIn(auth.AddUser("admin", UserRole.Admin));

        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True((await auth.Access.CheckAsync(permission, Ct)).Allowed, permission.ToString());
        }

        var page = await new SearchUsersHandler(auth.Access, auth.Users).HandleAsync(new SearchUsersQuery(null, false), Ct);
        Assert.True(page.IsSuccess);
        Assert.Single(page.Value.Items);
    }

    [Fact]
    public async Task UnCajeroConUnaConcesionValida_PuedeUsarUnPermisoAutorizable()
    {
        var auth = new AuthFixture();
        var cashier = auth.AddUser("caja", UserRole.Cashier);
        var admin = auth.AddUser("admin", UserRole.Admin);
        auth.SignedIn(cashier);
        var grant = auth.Grants.Issue(Permission.CancelSales, cashier.Id, admin.Id);

        var allowed = await auth.Access.CheckAsync(Permission.CancelSales, grant, Ct);
        var reused = await auth.Access.CheckAsync(Permission.CancelSales, grant, Ct);
        var otherPermission = await auth.Access.CheckAsync(Permission.ManageUsers, auth.Grants.Issue(Permission.CancelSales, cashier.Id, admin.Id), Ct);

        Assert.True(allowed.Allowed);
        Assert.Equal(admin.Id, allowed.AuthorizedBy);
        Assert.False(reused.Allowed);
        Assert.False(otherPermission.Allowed);
    }
}
