using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Sales;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Application.Users.UpdateUser;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Users;

public class UpdateUserHandlerTests
{
    private readonly AuthFixture _auth = new();
    private readonly InMemorySaleDraftStore _drafts = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private UpdateUserHandler Handler(IAccessControl? access = null) =>
        new(access ?? _auth.Access, _auth.Session, _auth.Users, _drafts, _auth.Audit, _auth.Transactions, new UpdateUserValidator(), NullLogger<UpdateUserHandler>.Instance);

    private static UpdateUserCommand Command(Pos.Domain.Users.User user, UserRole? role = null, bool? active = null, string? userName = null) =>
        new(user.Id, user.Version, user.FullName, userName ?? user.UserName, role ?? user.Role, active ?? user.IsActive);

    [Fact]
    public async Task UnAdministrador_NoPuedeDesactivarseNiQuitarseElRolASiMismo()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        _auth.AddUser("otro", UserRole.Admin);
        _auth.SignedIn(admin);

        var deactivate = await Handler().HandleAsync(Command(admin, active: false), Ct);
        var demote = await Handler().HandleAsync(Command(admin, role: UserRole.Cashier), Ct);

        Assert.IsType<LastAdministrator>(deactivate.Error);
        Assert.IsType<LastAdministrator>(demote.Error);
        Assert.True(admin.IsActive);
        Assert.Equal(UserRole.Admin, admin.Role);
    }

    [Fact]
    public async Task NuncaSeDejaAlSistemaSinAdministradorActivo()
    {
        var only = _auth.AddUser("unico", UserRole.Admin);

        var result = await Handler(new AllowAllAccessControl()).HandleAsync(Command(only, active: false), Ct);

        Assert.IsType<LastAdministrator>(result.Error);
        Assert.True(only.IsActive);
    }

    [Fact]
    public async Task ConOtroAdministradorActivo_SePuedeDesactivarAUnAdministrador()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var other = _auth.AddUser("otro", UserRole.Admin);
        _auth.SignedIn(admin);

        var result = await Handler().HandleAsync(Command(other, active: false), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(other.IsActive);
    }

    [Fact]
    public async Task AlDesactivar_DescartaLaVentaConservadaYLoAudita()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var cashier = _auth.AddUser("ana", UserRole.Cashier);
        _auth.SignedIn(admin);
        _drafts.Drafts[cashier.Id] = new StoredDraft(Guid.NewGuid(), [new DraftLineDto(Guid.NewGuid(), 1000, 100)]);

        var result = await Handler().HandleAsync(Command(cashier, active: false), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(_drafts.Drafts.ContainsKey(cashier.Id));
        Assert.Contains(_auth.Audit.Entries, e => e.Action == AuditActions.HeldSaleDiscarded && e.EntityId == cashier.Id);
        Assert.Contains(_auth.Audit.Entries, e => e.Action == AuditActions.UserDeactivated && e.EntityId == cashier.Id);
        Assert.Equal(1, _auth.Transactions.Commits);
    }

    [Fact]
    public async Task VersionDesactualizada_DevuelveConflict()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var cashier = _auth.AddUser("ana", UserRole.Cashier);
        _auth.SignedIn(admin);

        var result = await Handler().HandleAsync(Command(cashier) with { ExpectedVersion = cashier.Version + 1 }, Ct);

        Assert.IsType<Conflict>(result.Error);
    }

    [Fact]
    public async Task NombreDeUsuarioYaUsado_SinDistinguirMayusculas_DevuelveDuplicate()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var cashier = _auth.AddUser("ana", UserRole.Cashier);
        _auth.AddUser("Maria", UserRole.Cashier);
        _auth.SignedIn(admin);

        var result = await Handler().HandleAsync(Command(cashier, userName: "MARIA"), Ct);

        Assert.Equal("UserName", Assert.IsType<Duplicate>(result.Error).Field);
    }

    [Fact]
    public async Task UnCajero_NoPuedeEditarUsuarios()
    {
        var cashier = _auth.AddUser("ana", UserRole.Cashier);
        var admin = _auth.AddUser("admin", UserRole.Admin);
        _auth.SignedIn(cashier);

        var result = await Handler().HandleAsync(Command(admin, role: UserRole.Cashier), Ct);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(UserRole.Admin, admin.Role);
    }
}
