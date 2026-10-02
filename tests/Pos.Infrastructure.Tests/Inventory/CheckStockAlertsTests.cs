using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory.CheckStockAlerts;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Reports;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>
/// Revisión de alertas de existencia sobre SQLite real (022, research §14): una vez al día por producto,
/// nivel y usuario, con escalamiento alerta → urgente y purga de registros de más de 7 días.
/// </summary>
public sealed class CheckStockAlertsTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.CreateVersion7();
    private static readonly Guid Cashier = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 2);

    private TestDb _db = null!;
    private Product _urgent = null!;
    private Product _alert = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _db.Clock.UtcNow = new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);
        _db.User.UserId = Admin;

        _urgent = await SeedAsync("A-URG", "3");
        _alert = await SeedAsync("B-ALE", "12");
        await SeedAsync("D-OK", "50");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PrimeraRevision_NotificaAmbosNivelesYRegistra()
    {
        var check = await CheckAsync();

        Assert.Equal(new StockAlertCheck(UrgentCount: 1, AlertCount: 1, NotifyUrgent: true, NotifyAlert: true), check);
        await using var context = _db.CreateDbContext();
        var rows = await context.StockAlertAcknowledgements.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal((Admin, Today), (r.UserId, r.LocalDate)));
        Assert.Contains(rows, r => r.ProductId == _urgent.Id && r.Level == StockAlertLevel.Urgent);
        Assert.Contains(rows, r => r.ProductId == _alert.Id && r.Level == StockAlertLevel.Alert);
    }

    [Fact]
    public async Task SegundaRevisionDelMismoDia_NoNotifica()
    {
        await CheckAsync();
        _db.Clock.UtcNow = _db.Clock.UtcNow.AddHours(1);

        var check = await CheckAsync();

        Assert.Equal(new StockAlertCheck(1, 1, NotifyUrgent: false, NotifyAlert: false), check);
        Assert.Equal(2, await CountAcknowledgementsAsync());
    }

    [Fact]
    public async Task OtroUsuario_SiNotifica()
    {
        await CheckAsync();
        _db.User.UserId = Cashier;

        var check = await CheckAsync();

        Assert.True(check.NotifyUrgent);
        Assert.True(check.NotifyAlert);
        Assert.Equal(4, await CountAcknowledgementsAsync());
    }

    [Fact]
    public async Task DiaSiguiente_VuelveANotificar()
    {
        await CheckAsync();
        _db.Clock.UtcNow = _db.Clock.UtcNow.AddDays(1);

        var check = await CheckAsync();

        Assert.True(check.NotifyUrgent);
        Assert.True(check.NotifyAlert);
    }

    [Fact]
    public async Task AlertaQueBajaAUrgente_NotificaSoloUrgente()
    {
        await CheckAsync();
        await MoveOutAsync(_alert, "8");

        var check = await CheckAsync();

        Assert.Equal(new StockAlertCheck(UrgentCount: 2, AlertCount: 0, NotifyUrgent: true, NotifyAlert: false), check);
        await using var context = _db.CreateDbContext();
        Assert.True(await context.StockAlertAcknowledgements.AnyAsync(
            a => a.ProductId == _alert.Id && a.Level == StockAlertLevel.Urgent, Ct));
    }

    [Fact]
    public async Task UrgenteQueSubeAAlerta_NoNotifica()
    {
        await CheckAsync();
        await using (var context = _db.CreateDbContext())
        {
            var result = await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(_urgent.Id, MovementType.AdjustIn, "5", "conteo", null), Ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        var check = await CheckAsync();

        Assert.Equal(new StockAlertCheck(UrgentCount: 0, AlertCount: 2, NotifyUrgent: false, NotifyAlert: false), check);
    }

    [Fact]
    public async Task RegistrosDeMasDeSieteDias_SePurgan()
    {
        await using (var context = _db.CreateDbContext())
        {
            context.StockAlertAcknowledgements.AddRange(
                StockAlertAcknowledgement.Create(Cashier, _urgent.Id, StockAlertLevel.Urgent, Today.AddDays(-8), _db.Clock.UtcNow.AddDays(-8)),
                StockAlertAcknowledgement.Create(Cashier, _urgent.Id, StockAlertLevel.Urgent, Today.AddDays(-7), _db.Clock.UtcNow.AddDays(-7)));
            await context.SaveChangesAsync(Ct);
        }

        await CheckAsync();

        await using var check = _db.CreateDbContext();
        var dates = await check.StockAlertAcknowledgements.AsNoTracking().Select(a => a.LocalDate).ToListAsync(Ct);
        Assert.DoesNotContain(Today.AddDays(-8), dates);
        Assert.Contains(Today.AddDays(-7), dates);
    }

    [Fact]
    public async Task SinSesion_EsProhibido()
    {
        _db.User.UserId = Guid.Empty;

        await using var context = _db.CreateDbContext();
        var result = await Handler(context).HandleAsync(Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await CountAcknowledgementsAsync());
    }

    private CheckStockAlertsHandler Handler(PosDbContext context) => new(
        new AllowAllAccessControl(),
        _db.User,
        new StockAlertStore(context),
        new WriteTransactions(context),
        _db.Clock,
        new ReportPeriodResolver(TimeZoneInfo.Utc));

    private async Task<StockAlertCheck> CheckAsync()
    {
        await using var context = _db.CreateDbContext();
        var result = await Handler(context).HandleAsync(Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    private async Task<int> CountAcknowledgementsAsync()
    {
        await using var context = _db.CreateDbContext();
        return await context.StockAlertAcknowledgements.CountAsync(Ct);
    }

    /// <summary>Producto con mínimo 20 y punto de reorden 5, con la existencia inicial indicada.</summary>
    private async Task<Product> SeedAsync(string sku, string initial)
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, sku, minimumThousandths: 20_000, reorderPointThousandths: 5_000);
        await using var context = _db.CreateDbContext();
        var result = await InventoryTestSupport.Handler(context)
            .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, initial, null, null), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return product;
    }

    private async Task MoveOutAsync(Product product, string quantity)
    {
        await using var context = _db.CreateDbContext();
        var result = await InventoryTestSupport.Handler(context)
            .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.AdjustOut, quantity, "merma", null), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }
}
