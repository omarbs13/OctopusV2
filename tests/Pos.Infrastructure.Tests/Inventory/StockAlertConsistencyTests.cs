using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>
/// Consistencia de inventario (022, SC-005, obligatoria): el conteo SQL de <c>CountAlertsAsync</c>, la
/// regla <see cref="StockAlertRule"/> en memoria y las filas del reporte de inventario de hoy filtrado por
/// nivel coinciden.
/// </summary>
public sealed class StockAlertConsistencyTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _db.Clock.UtcNow = new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

        // Existencia / mínimo / punto de reorden.
        await SeedAsync("AL-URG", "3", minimum: 20_000, reorder: 5_000);
        await SeedAsync("AL-URG-EQ", "5", minimum: 20_000, reorder: 5_000);
        await SeedAsync("AL-ALE", "12", minimum: 20_000, reorder: 5_000);
        await SeedAsync("AL-ALE-EQ", "20", minimum: 20_000, reorder: 5_000);
        await SeedAsync("AL-OUT-NOREORDER", null, minimum: 10_000, reorder: null);
        await SeedAsync("AL-OUT-REORDER0", null, minimum: 10_000, reorder: 0);
        await SeedAsync("AL-OK", "50", minimum: 20_000, reorder: 5_000);
        await SeedAsync("AL-NOTHRESHOLD", "1", minimum: null, reorder: null);
        await SeedAsync("AL-ONLY-REORDER", "2", minimum: null, reorder: 2_000);
        await SeedAsync("AL-INACTIVE", null, minimum: 20_000, reorder: 5_000, active: false);
        await InventoryTestSupport.SeedProductAsync(_db, "AL-NOTRACK", tracks: false);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ConteoSql_ReglaEnMemoria_YReporteFiltrado_Coinciden()
    {
        await using var context = _db.CreateDbContext();
        var counts = await new InventoryRepository(context).CountAlertsAsync(Ct);

        var products = await context.Products.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.TracksInventory && p.IsActive)
            .ToListAsync(Ct);
        var stocks = await context.ProductStocks.AsNoTracking().ToDictionaryAsync(s => s.ProductId, s => s.OnHandThousandths, Ct);
        var levels = products.ToDictionary(
            p => p.Sku,
            p => StockAlertRule.Evaluate(
                StockLevel.FromThousandths(stocks.GetValueOrDefault(p.Id)), p.MinimumStock, p.ReorderPoint));

        var end = _db.Clock.UtcNow.AddDays(1);
        var reader = new InventoryReportReader(context);
        var today = DateOnly.FromDateTime(_db.Clock.UtcNow);
        var urgentRows = (await reader.GetAsync(end, new InventoryReportQuery(today, StockFilter.Urgent), Ct)).Rows;
        var alertRows = (await reader.GetAsync(end, new InventoryReportQuery(today, StockFilter.Alert), Ct)).Rows;

        string[] expectedUrgent = ["AL-ONLY-REORDER", "AL-OUT-REORDER0", "AL-URG", "AL-URG-EQ"];
        string[] expectedAlert = ["AL-ALE", "AL-ALE-EQ", "AL-OUT-NOREORDER"];
        Assert.Equal(expectedUrgent, levels.Where(l => l.Value == StockAlertLevel.Urgent).Select(l => l.Key).Order(StringComparer.Ordinal));
        Assert.Equal(expectedAlert, levels.Where(l => l.Value == StockAlertLevel.Alert).Select(l => l.Key).Order(StringComparer.Ordinal));

        Assert.Equal(expectedUrgent.Length, counts.Urgent);
        Assert.Equal(expectedAlert.Length, counts.Alert);
        Assert.Equal(expectedUrgent, urgentRows.Select(r => r.Sku).Order(StringComparer.Ordinal));
        Assert.Equal(expectedAlert, alertRows.Select(r => r.Sku).Order(StringComparer.Ordinal));
        Assert.All(urgentRows, r => Assert.Equal(StockAlertLevel.Urgent, r.Level));

        // El listado de existencias con el mismo filtro usa el mismo predicado SQL.
        var repository = new InventoryRepository(context);
        Assert.Equal(counts.Urgent, (await repository.SearchStockAsync(Search(StockFilter.Urgent), Ct)).TotalCount);
        Assert.Equal(counts.Alert, (await repository.SearchStockAsync(Search(StockFilter.Alert), Ct)).TotalCount);
    }

    [Fact]
    public async Task AgotadoConPuntoDeReorden_EsUrgenteYSinExistencia()
    {
        await using var context = _db.CreateDbContext();
        var today = DateOnly.FromDateTime(_db.Clock.UtcNow);
        var report = await new InventoryReportReader(context).GetAsync(_db.Clock.UtcNow.AddDays(1), new InventoryReportQuery(today), Ct);

        var row = report.Rows.Single(r => r.Sku == "AL-OUT-REORDER0");
        Assert.Equal(StockStatus.Out, row.Status);
        Assert.Equal(StockAlertLevel.Urgent, row.Level);
        Assert.Equal(0, row.ReorderPointThousandths);
        Assert.Equal(StockAlertLevel.None, report.Rows.Single(r => r.Sku == "AL-INACTIVE").Level);
    }

    private static StockSearch Search(StockFilter filter) => new(null, null, null, false, filter, false, 1, 100);

    private async Task SeedAsync(string sku, string? initial, long? minimum, long? reorder, bool active = true)
    {
        var product = await InventoryTestSupport.SeedProductAsync(
            _db, sku, minimumThousandths: minimum, reorderPointThousandths: reorder);
        if (initial is not null)
        {
            await using var context = _db.CreateDbContext();
            var result = await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, initial, null, null), Ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        if (!active)
        {
            await using var context = _db.CreateDbContext();
            var tracked = await context.Products.FindAsync([product.Id], Ct);
            tracked!.Update(
                tracked.Name, tracked.Sku, null, tracked.Price, "H87", isActive: false, tracked.TracksInventory, tracked.MinimumStock,
                reorderPoint: tracked.ReorderPoint);
            await context.SaveChangesAsync(Ct);
        }
    }
}
