using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Users;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>SC-006: una cancelación autorizada desde la sesión de un cajero registra a ambos usuarios.</summary>
public sealed class CancelSaleAuthorizationTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private CancelSaleHandler Handler(PosDbContext context, IUserSession session, IAuthorizationGrants grants) =>
        new(
            new AccessControl(session, new UserRepository(context), grants, NullLogger<AccessControl>.Instance),
            new SaleRepository(context),
            new CashShiftRepository(context),
            new InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new CancelSaleValidator(),
            NullLogger<CancelSaleHandler>.Instance);

    [Fact]
    public async Task ElCajeroCancelaConLaConcesionDeUnAdministrador_QuedanAmbosEnLaBitacora()
    {
        var cashier = User.Create("Caja Uno", "caja", UserRole.Cashier, "hash");
        var admin = User.Create("Admin Uno", "admin", UserRole.Admin, "hash");
        await using (var context = _db.CreateDbContext())
        {
            context.Users.AddRange(cashier, admin);
            await context.SaveChangesAsync(Ct);
        }

        _db.User.UserId = cashier.Id;
        var product = await SalesTestSupport.SeedProductAsync(_db, "AUT-1");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        var session = new FixedSession(SessionUser.From(cashier));
        var grants = new AuthorizationGrants(_db.Clock);
        int version;
        await using (var read = _db.CreateDbContext())
        {
            version = (await read.Sales.AsNoTracking().SingleAsync(Ct)).Version;
        }

        // Sin concesión: rechazado y autorizable, sin efectos.
        await using (var context = _db.CreateDbContext())
        {
            var denied = await Handler(context, session, grants)
                .HandleAsync(new CancelSaleCommand(sale.SaleId, version, "Error"), Ct);
            var forbidden = Assert.IsType<Forbidden>(denied.Error);
            Assert.True(forbidden.CanBeAuthorized);
        }

        // Con la concesión del administrador: se cancela a nombre del cajero.
        var grant = grants.Issue(Permission.CancelSales, cashier.Id, admin.Id);
        await using (var context = _db.CreateDbContext())
        {
            var result = await Handler(context, session, grants)
                .HandleAsync(new CancelSaleCommand(sale.SaleId, version, "Error", grant), Ct);
            Assert.True(result.IsSuccess);
        }

        await using var check = _db.CreateDbContext();
        var stored = await check.Sales.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(SaleStatus.Cancelled, stored.Status);
        Assert.Equal(cashier.Id, stored.CancelledBy);
        var entry = await check.AuditEntries.AsNoTracking().SingleAsync(a => a.Action == AuditActions.SaleCancelled, Ct);
        Assert.Equal(cashier.Id, entry.CreatedBy);
        Assert.Equal(admin.Id, entry.AuthorizedBy);
    }

    private sealed class FixedSession : IUserSession
    {
        public FixedSession(SessionUser user) => User = user;

        public SessionUser? User { get; }

        public Guid UserId => User!.Id;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }
}
