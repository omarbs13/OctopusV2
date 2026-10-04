using Pos.Application.Audit;
using Pos.Domain.Audit;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>
/// Consulta de la bitácora (FR-027): orden, filtros, usuario involucrado y nombres. Desde 018: filtro por
/// entidad, historial de un registro y entradas anteriores sin cambios (FR-017, FR-021).
/// </summary>
public sealed class AuditLogReaderTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
    private static readonly AuditFilter NoFilter = new(null, null, null, null, null, null);

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

    private async Task AddAsync(
        Guid author,
        DateTime at,
        string action,
        Guid entityId,
        Guid? authorizedBy = null,
        string entityType = "User",
        IEnumerable<AuditFieldChange>? changes = null)
    {
        _db.User.UserId = author;
        _db.Clock.UtcNow = at;
        await using var context = _db.CreateDbContext();
        context.AuditEntries.Add(AuditEntry.Create(action, entityType, entityId, "detalle", authorizedBy, changes: changes));
        await context.SaveChangesAsync(Ct);
    }

    private async Task<AuditPage> SearchAsync(AuditFilter filter, int page = 1)
    {
        await using var context = _db.CreateDbContext();
        return await new AuditLogReader(context).SearchAsync(new AuditSearch(filter, page, 100), Ct);
    }

    [Fact]
    public async Task Orden_MasRecientePrimero_ConNombresDeAutorYAutorizador()
    {
        await AddAsync(_cashier.Id, T0, AuditActions.LoginSucceeded, _cashier.Id);
        await AddAsync(_cashier.Id, T0.AddMinutes(5), AuditActions.AdminAuthorizationGranted, _admin.Id, _admin.Id);

        var page = await SearchAsync(NoFilter);

        Assert.Equal([AuditActions.AdminAuthorizationGranted, AuditActions.LoginSucceeded], page.Items.Select(r => r.Action));
        Assert.Equal("caja", page.Items[0].UserName);
        Assert.Equal("admin", page.Items[0].AuthorizedByName);
        Assert.Null(page.Items[1].AuthorizedByName);
    }

    [Fact]
    public async Task Filtros_FechasAccionYUsuarioInvolucrado()
    {
        await AddAsync(_cashier.Id, T0, AuditActions.LoginSucceeded, _cashier.Id);
        await AddAsync(_admin.Id, T0.AddHours(1), AuditActions.UserDeactivated, _cashier.Id);
        await AddAsync(_admin.Id, T0.AddHours(2), AuditActions.PasswordReset, _admin.Id);

        var byRange = await SearchAsync(NoFilter with { FromUtc = T0.AddMinutes(30), ToUtcExclusive = T0.AddHours(2) });
        var byAction = await SearchAsync(NoFilter with { Action = AuditActions.PasswordReset });
        var involvingCashier = await SearchAsync(NoFilter with { UserId = _cashier.Id });

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

        var first = await SearchAsync(NoFilter);
        var second = await SearchAsync(NoFilter, page: 2);

        Assert.Equal(105, first.TotalCount);
        Assert.Equal(100, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Equal(2, first.TotalPages);
    }

    [Fact]
    public async Task Entidad_ProductoYSesion_SeparanLosEventosDeUsuario()
    {
        var product = Guid.CreateVersion7();
        await AddAsync(_admin.Id, T0, AuditActions.ProductUpdated, product, entityType: AuditActions.ProductEntity);
        await AddAsync(_cashier.Id, T0.AddMinutes(1), AuditActions.LoginSucceeded, _cashier.Id);
        await AddAsync(_admin.Id, T0.AddMinutes(2), AuditActions.UserUpdated, _cashier.Id);

        var products = await SearchAsync(NoFilter with { Entity = AuditEntityGroup.Product });
        var sessions = await SearchAsync(NoFilter with { Entity = AuditEntityGroup.Session });
        var users = await SearchAsync(NoFilter with { Entity = AuditEntityGroup.User });

        Assert.Equal([AuditActions.ProductUpdated], products.Items.Select(r => r.Action));
        Assert.Equal([AuditActions.LoginSucceeded], sessions.Items.Select(r => r.Action));
        Assert.Equal([AuditActions.UserUpdated], users.Items.Select(r => r.Action));
    }

    [Fact]
    public async Task Entidad_Configuracion_IncluyeElUmbralDeReportesQueNoEstaEnReportes()
    {
        await AddAsync(_admin.Id, T0, AuditActions.ReportSettingsChanged, Guid.CreateVersion7(), entityType: AuditActions.ReportEntity);
        await AddAsync(_admin.Id, T0.AddMinutes(1), AuditActions.ReportExported, Guid.CreateVersion7(), entityType: AuditActions.ReportEntity);
        await AddAsync(_admin.Id, T0.AddMinutes(2), AuditActions.DiscountLimitChanged, Guid.CreateVersion7(), entityType: AuditActions.DiscountSettingsEntity);

        var settings = await SearchAsync(NoFilter with { Entity = AuditEntityGroup.Settings });
        var reports = await SearchAsync(NoFilter with { Entity = AuditEntityGroup.Reports });

        Assert.Equal([AuditActions.DiscountLimitChanged, AuditActions.ReportSettingsChanged], settings.Items.Select(r => r.Action));
        Assert.Equal([AuditActions.ReportExported], reports.Items.Select(r => r.Action));
    }

    [Fact]
    public async Task HistorialDelRegistro_DeLaMasAntiguaALaMasReciente_ConSusCambios()
    {
        var product = Guid.CreateVersion7();
        await AddAsync(_admin.Id, T0, AuditActions.ProductCreated, product, entityType: AuditActions.ProductEntity, changes: [new("Precio", null, "$25.00")]);
        await AddAsync(_admin.Id, T0.AddHours(1), AuditActions.ProductUpdated, product, entityType: AuditActions.ProductEntity, changes: [new("Precio", "$25.00", "$28.50")]);
        await AddAsync(_admin.Id, T0.AddHours(2), AuditActions.ProductUpdated, Guid.CreateVersion7(), entityType: AuditActions.ProductEntity);

        var history = await SearchAsync(NoFilter with { Record = new AuditRecordRef(AuditActions.ProductEntity, product) });

        Assert.Equal([AuditActions.ProductCreated, AuditActions.ProductUpdated], history.Items.Select(r => r.Action));
        var change = Assert.Single(history.Items[1].Changes);
        Assert.Equal(("Precio", "$25.00", "$28.50"), (change.Field, change.Before, change.After));
    }

    [Fact]
    public async Task UsuarioInvolucrado_EncuentraAlAutorizador()
    {
        await AddAsync(_cashier.Id, T0, AuditActions.SaleCancelled, Guid.CreateVersion7(), _admin.Id, AuditActions.SaleEntity);
        await AddAsync(_cashier.Id, T0.AddMinutes(1), AuditActions.SaleReturned, Guid.CreateVersion7(), entityType: AuditActions.SaleEntity);

        var page = await SearchAsync(NoFilter with { UserId = _admin.Id });

        Assert.Equal([AuditActions.SaleCancelled], page.Items.Select(r => r.Action));
    }

    [Fact]
    public async Task EntradasAnteriores_SinCambios_SeIncluyenPorSuEntidad()
    {
        // Entrada como las de antes de 0.13.0: sin nombre, motivo ni cambios.
        await AddAsync(_cashier.Id, T0, AuditActions.SaleCancelled, Guid.CreateVersion7(), entityType: AuditActions.SaleEntity);

        var row = Assert.Single((await SearchAsync(NoFilter with { Entity = AuditEntityGroup.Sale })).Items);

        Assert.Null(row.EntityName);
        Assert.Null(row.Reason);
        Assert.Empty(row.Changes);
        Assert.Equal("detalle", row.Details);
    }
}
