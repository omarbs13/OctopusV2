using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Reports;
using Pos.Application.Reports.GetReportAlerts;
using Pos.Application.Reports.SetProductCritical;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>Historia 7 con SQLite real: turnos con diferencia sobre el umbral y productos críticos con existencia baja.</summary>
public sealed class ReportAlertsQueryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListaTurnosSobreElUmbralYProductosCriticosConExistenciaBaja()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await ShiftTestSupport.CreateAsync(db);
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc);

        // Turno cerrado con faltante de 6 %.
        shifts.As(shifts.Cashier);
        var shift = (await shifts.OpenAsync(100_000)).Value;
        var count = (await shifts.CountAsync(shift.ShiftId, 94_000)).Value;
        Assert.True((await shifts.CloseAsync(shift.ShiftId, count.Version, 94_000, count.ExpectedCents, "Faltante")).IsSuccess);

        // Críticos: uno con existencia baja, uno normal y uno agotado; y uno no crítico con existencia baja.
        var low = await InventoryTestSupport.SeedProductAsync(db, "CRI-LOW", minimumThousandths: 5_000);
        var normal = await InventoryTestSupport.SeedProductAsync(db, "CRI-OK", minimumThousandths: 5_000);
        var empty = await InventoryTestSupport.SeedProductAsync(db, "CRI-OUT", minimumThousandths: 5_000);
        var plain = await InventoryTestSupport.SeedProductAsync(db, "NO-CRI", minimumThousandths: 5_000);
        await SalesTestSupport.StockAsync(db, low, "2");
        await SalesTestSupport.StockAsync(db, normal, "20");
        await SalesTestSupport.StockAsync(db, empty, "1");
        await SalesTestSupport.StockAsync(db, plain, "1");
        await using (var context = db.CreateDbContext())
        {
            var out1 = await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(empty.Id, MovementType.AdjustOut, "1", "Merma", null), Ct);
            Assert.True(out1.IsSuccess);
        }

        foreach (var product in new[] { low, normal, empty })
        {
            await using var context = db.CreateDbContext();
            Assert.True((await new SetProductCriticalHandler(new AllowAllAccessControl(), new ProductRepository(context))
                .HandleAsync(new SetProductCriticalCommand(product.Id, true), Ct)).IsSuccess);
        }

        await using var read = db.CreateDbContext();
        var handler = new GetReportAlertsHandler(
            new AllowAllAccessControl(),
            new CashCountReportReader(read),
            new ReportAlertsReader(read),
            new Settings(),
            new ReportPeriodResolver(TimeZoneInfo.Utc),
            db.Clock);

        var alerts = (await handler.HandleAsync(Ct)).Value;

        Assert.Equal(1, alerts.CashAlertTotal);
        Assert.Equal(-6_000, alerts.CashAlerts.Single().DifferenceCents);
        Assert.Equal(2, alerts.CriticalLowStockTotal);
        Assert.Equal(["CRI-LOW", "CRI-OUT"], alerts.CriticalLowStock.Select(a => a.Sku).Order());
        Assert.Equal(StockStatus.Out, alerts.CriticalLowStock.Single(a => a.Sku == "CRI-OUT").Status);
        Assert.False(alerts.IsEmpty);
    }

    [Fact]
    public async Task SinAlertas_EstaVacio()
    {
        using var db = await TestDb.CreateAsync();
        await using var read = db.CreateDbContext();
        var handler = new GetReportAlertsHandler(
            new AllowAllAccessControl(),
            new CashCountReportReader(read),
            new ReportAlertsReader(read),
            new Settings(),
            new ReportPeriodResolver(TimeZoneInfo.Utc),
            db.Clock);

        Assert.True((await handler.HandleAsync(Ct)).Value.IsEmpty);
    }

    private sealed class Settings : IReportSettingsStore
    {
        public ReportSettings Load() => new();

        public void Save(ReportSettings settings)
        {
        }
    }
}
