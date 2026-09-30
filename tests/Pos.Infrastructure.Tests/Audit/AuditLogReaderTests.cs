using Pos.Application.Audit;
using Pos.Domain.Audit;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>Consulta de la bitácora (FR-027): orden, filtros, usuario involucrado y nombres.</summary>
public sealed class AuditLogReaderTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private User _cashier = null!;
    private User _admin = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _cashier = User.Create("Caja Uno", "caja", UserRole.Cashier, "hash");
        _admin = User.Create("Admin Uno", "admin", UserRole.Admin, "hash");
        await using var context = _db.CreateDbContext();
        context.Users.AddRange(_cashier, _admin);
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task AddAsync(Guid author, DateTime at, string action, Guid entityId, Guid? authorizedBy = null, string entityType = "User")
    {
        _db.User.UserId = author;
        _db.Clock.UtcNow = at;
        await using var context = _db.CreateDbContext();
        context.AuditEntries.Add(AuditEntry.Create(action, entityType, entityId, "detalle", authorizedBy));
        await context.SaveChangesAsync(Ct);
    }

    private async Task<AuditPage> SearchAsync(AuditSearch search)
    {
        await using var context = _db.CreateDbContext();
        return await new AuditLogReader(context).SearchAsync(search, Ct);
    }

    private static readonly DateTime T0 = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Orden_MasRecientePrimero_ConNombresDeAutorYAutorizador()
    {
        await AddAsync(_cashier.Id, T0, AuditActions.LoginSucceeded, _cashier.Id);
        await AddAsync(_cashier.Id, T0.AddMinutes(5), AuditActions.AdminAuthorizationGranted, _admin.Id, _admin.Id);

        var page = await SearchAsync(new AuditSearch(null, null, null, null, 1, 100));

        Assert.Equal([AuditActions.AdminAuthorizationGranted, AuditActions.LoginSucceeded], page.Items.Select(r => r.Action));
        Assert.Equal("Caja Uno", page.Items[0].UserName);
        Assert.Equal("Admin Uno", page.Items[0].AuthorizedByName);
        Assert.Null(page.Items[1].AuthorizedByName);
    }

    [Fact]
    public async Task Filtros_FechasAccionYUsuarioInvolucrado()
    {
        await AddAsync(_cashier.Id, T0, AuditActions.LoginSucceeded, _cashier.Id);
        await AddAsync(_admin.Id, T0.AddHours(1), AuditActions.UserDeactivated, _cashier.Id);
        await AddAsync(_admin.Id, T0.AddHours(2), AuditActions.PasswordReset, _admin.Id);

        var byRange = await SearchAsync(new AuditSearch(T0.AddMinutes(30), T0.AddHours(2), null, null, 1, 100));
        var byAction = await SearchAsync(new AuditSearch(null, null, null, AuditActions.PasswordReset, 1, 100));
        var involvingCashier = await SearchAsync(new AuditSearch(null, null, _cashier.Id, null, 1, 100));

        // El límite superior es exclusivo: la entrada de T0 + 2 h queda fuera.
        Assert.Equal([AuditActions.UserDeactivated], byRange.Items.Select(r => r.Action));
        Assert.Equal([AuditActions.PasswordReset], byAction.Items.Select(r => r.Action));

        // Involucrado: autor de su inicio de sesión y afectado por la desactivación.
        Assert.Equal([AuditActions.UserDeactivated, AuditActions.LoginSucceeded], involvingCashier.Items.Select(r => r.Action));
    }

    [Fact]
    public async Task Pagina_DeCienRegistrosYTotal()
    {
        for (var i = 0; i < 105; i++)
        {
            await AddAsync(_admin.Id, T0.AddSeconds(i), AuditActions.LoginSucceeded, _admin.Id);
        }

        var first = await SearchAsync(new AuditSearch(null, null, null, null, 1, 100));
        var second = await SearchAsync(new AuditSearch(null, null, null, null, 2, 100));

        Assert.Equal(105, first.TotalCount);
        Assert.Equal(100, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Equal(2, first.TotalPages);
    }
}
