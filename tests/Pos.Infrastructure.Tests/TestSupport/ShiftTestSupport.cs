using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.CloseShift;
using Pos.Application.CashShifts.CountShiftCash;
using Pos.Application.CashShifts.OpenShift;
using Pos.Application.CashShifts.RegisterCashMovement;
using Pos.Application.Licensing;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Application.Users.Access;
using Pos.Application.Users;
using Pos.Application.Users.Session;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Arma los casos de uso de turnos sobre SQLite real con usuarios y permisos reales: un administrador
/// y un cajero, y un ámbito (contexto) por operación como la composición.
/// </summary>
public sealed class ShiftTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;

    private ShiftTestSupport(TestDb db, User admin, User cashier)
    {
        _db = db;
        Admin = admin;
        Cashier = cashier;
        Grants = new AuthorizationGrants(db.Clock);
        As(admin);
    }

    public User Admin { get; }

    public User Cashier { get; }

    public AuthorizationGrants Grants { get; }

    /// <summary>Estado de licencia de las operaciones; nulo = sin restricciones.</summary>
    public ILicenseState? License { get; set; }

    public static async Task<ShiftTestSupport> CreateAsync(TestDb db)
    {
        var admin = User.Create("Admin Uno", "admin", UserRole.Admin, "hash");
        var cashier = User.Create("Caja Uno", "caja", UserRole.Cashier, "hash");
        await using var context = db.CreateDbContext();
        context.Users.AddRange(admin, cashier);
        await context.SaveChangesAsync(Ct);
        return new ShiftTestSupport(db, admin, cashier);
    }

    private IUserSession Session { get; set; } = null!;

    /// <summary>Cambia el usuario conectado para las siguientes operaciones.</summary>
    public void As(User user)
    {
        _db.User.UserId = user.Id;
        Session = new FixedSession(SessionUser.From(user));
    }

    public AccessControl Access(PosDbContext context) =>
        new(Session, new UserRepository(context), Grants, NullLogger<AccessControl>.Instance, License);

    public ShiftGuard Guard(PosDbContext context) => SalesTestSupport.ShiftGuardFor(_db, context);

    public async Task<Result<CurrentShiftSummary>> OpenAsync(long floatCents = 50_000, bool confirmZero = false)
    {
        await using var context = _db.CreateDbContext();
        return await new OpenShiftHandler(
            Access(context),
            new CashShiftRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new OpenShiftValidator(),
            Guard(context),
            NullLogger<OpenShiftHandler>.Instance).HandleAsync(new OpenShiftCommand(floatCents, confirmZero), Ct);
    }

    public async Task<Result<RegisteredMovement>> MoveAsync(
        Guid shiftId,
        CashMovementType type,
        long amountCents,
        string reason = "Motivo",
        Guid? grant = null)
    {
        await using var context = _db.CreateDbContext();
        return await new RegisterCashMovementHandler(
            Access(context),
            new CashShiftRepository(context),
            new SaleRepository(context),
            _db.User,
            new AuditLog(context),
            new WriteTransactions(context),
            new RegisterCashMovementValidator(),
            NullLogger<RegisterCashMovementHandler>.Instance)
            .HandleAsync(new RegisterCashMovementCommand(shiftId, type, amountCents, reason, grant), Ct);
    }

    public async Task<Result<ShiftCountResult>> CountAsync(Guid shiftId, long countedCents, bool discard = false)
    {
        await using var context = _db.CreateDbContext();
        return await new CountShiftCashHandler(
            Access(context),
            _db.User,
            new CashShiftRepository(context),
            new SaleRepository(context),
            Guard(context),
            new AuditLog(context),
            new WriteTransactions(context),
            NullLogger<CountShiftCashHandler>.Instance).HandleAsync(new CountShiftCashCommand(shiftId, countedCents, discard), Ct);
    }

    public async Task<Result<ClosedShift>> CloseAsync(
        Guid shiftId,
        int expectedVersion,
        long countedCents,
        long shownExpectedCents,
        string? comment = null,
        bool discard = false)
    {
        await using var context = _db.CreateDbContext();
        return await new CloseShiftHandler(
            Access(context),
            _db.User,
            new CashShiftRepository(context),
            new SaleRepository(context),
            new SqliteSaleDraftStore(context, _db.Clock, _db.User),
            Guard(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            NullLogger<CloseShiftHandler>.Instance)
            .HandleAsync(new CloseShiftCommand(shiftId, expectedVersion, countedCents, shownExpectedCents, comment, discard), Ct);
    }


    public async Task<Result> CancelAsync(Guid saleId, Guid? grant = null)
    {
        await using var context = _db.CreateDbContext();
        var version = (await new SaleRepository(context).GetAsync(saleId, Ct))!.Version;
        return await new CancelSaleHandler(
            Access(context),
            new SaleRepository(context),
            new CashShiftRepository(context),
            new Pos.Infrastructure.Inventory.InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new CancelSaleValidator(),
            NullLogger<CancelSaleHandler>.Instance).HandleAsync(new CancelSaleCommand(saleId, version, "Error", grant), Ct);
    }

    public async Task SaveDraftAsync(Pos.Domain.Products.Product product)
    {
        await using var context = _db.CreateDbContext();
        await SalesTestSupport.SaveDraftHandler(_db, context).HandleAsync(
            new SaveSaleDraftCommand(Guid.CreateVersion7(), [new DraftLineDto(product.Id, 1000, product.Price.Cents)]), Ct);
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
