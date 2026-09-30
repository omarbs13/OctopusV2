using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.AuthorizeAdmin;
using Pos.Application.Users.SignIn;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Users;

public class AuthorizeAdminHandlerTests
{
    private readonly AuthFixture _auth = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AuthorizeAdminHandler Handler =>
        new(_auth.Verifier, _auth.Session, _auth.Grants, new AuthorizeAdminValidator(), NullLogger<AuthorizeAdminHandler>.Instance);

    private Task<Result<Guid>> AuthorizeAsync(Permission permission, string userName, string password) =>
        Handler.HandleAsync(new AuthorizeAdminCommand(permission, userName, password), Ct);

    private (User Cashier, User Admin) Setup()
    {
        var cashier = _auth.AddUser("caja", UserRole.Cashier);
        var admin = _auth.AddUser("admin", UserRole.Admin);
        _auth.SignedIn(cashier);
        return (cashier, admin);
    }

    [Fact]
    public async Task Concesion_EsDeUnSoloUso()
    {
        var (cashier, admin) = Setup();

        var grant = (await AuthorizeAsync(Permission.CancelSales, "admin", AuthFixture.Password)).Value;

        Assert.Equal(admin.Id, _auth.Grants.TryConsume(grant, Permission.CancelSales, cashier.Id));
        Assert.Null(_auth.Grants.TryConsume(grant, Permission.CancelSales, cashier.Id));
    }

    [Fact]
    public async Task Concesion_EstaLigadaAlPermisoYAlSolicitante()
    {
        var (cashier, _) = Setup();
        var grant = (await AuthorizeAsync(Permission.CancelSales, "admin", AuthFixture.Password)).Value;

        Assert.Null(_auth.Grants.TryConsume(grant, Permission.OpenDrawerWithoutSale, cashier.Id));
        Assert.Null(_auth.Grants.TryConsume(grant, Permission.CancelSales, Guid.NewGuid()));
    }

    [Fact]
    public async Task Concesion_VenceALos2Minutos()
    {
        var (cashier, _) = Setup();
        var grant = (await AuthorizeAsync(Permission.CancelSales, "admin", AuthFixture.Password)).Value;

        _auth.Clock.UtcNow = _auth.Clock.UtcNow.AddMinutes(2).AddSeconds(1);

        Assert.Null(_auth.Grants.TryConsume(grant, Permission.CancelSales, cashier.Id));
    }

    [Fact]
    public async Task CredencialesDeUnNoAdministrador_DevuelvenInvalidCredentialsYQuedanEnLaBitacora()
    {
        Setup();
        _auth.AddUser("otra", UserRole.Cashier);

        var result = await AuthorizeAsync(Permission.CancelSales, "otra", AuthFixture.Password);

        Assert.IsType<InvalidCredentials>(result.Error);
        Assert.Contains(_auth.Audit.Entries, e => e.Action == AuditActions.AdminAuthorizationDenied);
    }

    [Fact]
    public async Task PermisoNoAutorizable_DevuelveInvalidState()
    {
        Setup();

        var result = await AuthorizeAsync(Permission.ManageUsers, "admin", AuthFixture.Password);

        Assert.IsType<InvalidState>(result.Error);
    }

    [Fact]
    public async Task CincoFallosDeUnAdministrador_LoBloqueanTambienParaIniciarSesion()
    {
        var (_, admin) = Setup();

        for (var i = 0; i < 5; i++)
        {
            await AuthorizeAsync(Permission.CancelSales, "admin", "mal");
        }

        var signIn = await new SignInHandler(_auth.Verifier, _auth.Session, new SignInValidator(), NullLogger<SignInHandler>.Instance)
            .HandleAsync(new SignInCommand("admin", AuthFixture.Password), Ct);
        var authorize = await AuthorizeAsync(Permission.CancelSales, "admin", AuthFixture.Password);

        Assert.True(admin.IsLockedOut(_auth.Clock.UtcNow));
        Assert.IsType<LockedOut>(signIn.Error);
        Assert.IsType<LockedOut>(authorize.Error);
    }

    [Fact]
    public async Task LaAutorizacionConcedida_AuditaAlAdministradorComoAutorizadorSinCambiarLaSesion()
    {
        var (cashier, admin) = Setup();

        await AuthorizeAsync(Permission.CancelSales, "admin", AuthFixture.Password);

        var entry = Assert.Single(_auth.Audit.Entries, e => e.Action == AuditActions.AdminAuthorizationGranted);
        Assert.Equal(admin.Id, entry.AuthorizedBy);
        Assert.Equal(cashier.Id, _auth.Session.User!.Id);
    }
}
