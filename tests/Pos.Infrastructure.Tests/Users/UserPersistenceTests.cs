using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Users.CreateFirstAdmin;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Security;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.Users;

public sealed class UserPersistenceTests : IDisposable
{
    private readonly TestDb _db = TestDb.CreateAsync().GetAwaiter().GetResult();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    private static User NewUser(string userName) => User.Create("Persona", userName, UserRole.Cashier, "hash");

    private CreateFirstAdminHandler FirstAdminHandler(PosDbContext context) =>
        new(
            new UserRepository(context),
            new Pbkdf2PasswordHasher(iterations: 1000),
            new SqliteSaleDraftStore(context, _db.Clock, _db.User),
            new AuditLog(context),
            new WriteTransactions(context),
            new CreateFirstAdminValidator(),
            NullLogger<CreateFirstAdminHandler>.Instance);

    [Fact]
    public async Task ElAsistenteEjecutadoDosVeces_SoloCreaUnUsuarioYElSegundoRecibeInvalidState()
    {
        async Task<Result> RunAsync(string userName)
        {
            await using var context = _db.CreateDbContext();
            return await FirstAdminHandler(context)
                .HandleAsync(new CreateFirstAdminCommand("Administrador", userName, "contraseña1", "contraseña1"), Ct);
        }

        var results = await Task.WhenAll(Task.Run(() => RunAsync("admin1"), Ct), Task.Run(() => RunAsync("admin2"), Ct));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.IsType<InvalidState>(Assert.Single(results, r => !r.IsSuccess).Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Users.CountAsync(u => !u.IsSystem, Ct));
    }

    [Fact]
    public async Task ElAsistente_ReasignaAlAdministradorLaVentaEnCursoDeSistema()
    {
        await using (var context = _db.CreateDbContext())
        {
            context.SaleDrafts.Add(Pos.Domain.Sales.SaleDraft.Create(SystemUser.Id, Guid.NewGuid(), "[]", _db.Clock.UtcNow));
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateDbContext())
        {
            var result = await FirstAdminHandler(context)
                .HandleAsync(new CreateFirstAdminCommand("Administrador", "admin", "contraseña1", "contraseña1"), Ct);
            Assert.True(result.IsSuccess);
        }

        await using var check = _db.CreateDbContext();
        var adminId = (await check.Users.SingleAsync(u => !u.IsSystem, Ct)).Id;
        Assert.Equal([adminId], await check.SaleDrafts.Select(d => d.UserId).ToListAsync(Ct));
    }

    [Fact]
    public async Task IndiceUnico_SinDistinguirMayusculas_ConDosAltasSimultaneas_UnaGanaYLaOtraRecibeDuplicate()
    {
        async Task<SaveOutcome> AddAsync(string userName)
        {
            await using var context = _db.CreateDbContext();
            var repository = new UserRepository(context);
            repository.Add(NewUser(userName));
            return await repository.SaveChangesAsync(Ct);
        }

        var outcomes = await Task.WhenAll(Task.Run(() => AddAsync("Maria"), Ct), Task.Run(() => AddAsync("MARIA"), Ct));

        Assert.Equal(1, outcomes.Count(o => o.Status == SaveStatus.Saved));
        var loser = Assert.Single(outcomes, o => o.Status != SaveStatus.Saved);
        Assert.Equal(SaveStatus.Duplicate, loser.Status);
        Assert.Equal("UserName", loser.DuplicateField);
    }

    [Fact]
    public async Task BloqueoPersistente_UnSegundoContextoVeElContadorYElFinDelBloqueo()
    {
        var user = NewUser("ana");
        await using (var context = _db.CreateDbContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync(Ct);
        }

        var until = new DateTime(2026, 9, 30, 12, 5, 0, DateTimeKind.Utc);
        await using (var context = _db.CreateDbContext())
        {
            await new UserRepository(context).RecordLoginAttemptAsync(user.Id, 5, until, null, Ct);
        }

        await using var other = _db.CreateDbContext();
        var reloaded = await new UserRepository(other).GetAsync(user.Id, Ct);
        Assert.Equal(5, reloaded!.FailedLoginCount);
        Assert.Equal(until, reloaded.LockoutEndsAt);
        Assert.Equal(1, reloaded.Version);
    }
}
