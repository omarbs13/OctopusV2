using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

public sealed class InventoryReportReaderTests
{
    private static DateTime Utc(int day, int hour = 10) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime EndOf(int day) => new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);

    [Fact]
    public async Task ExistenciaALaFecha_NoLaActual()
    {
        using var db = await TestDb.CreateAsync();
        await SeedAsync(db);

        var day15 = await GetAsync(db, EndOf(15), new InventoryReportQuery(new DateOnly(2026, 9, 15)));
        var day25 = await GetAsync(db, EndOf(25), new InventoryReportQuery(new DateOnly(2026, 9, 25)));

        // El día 15 el producto A aún tiene 10; el día 25 ya salió todo y aparece sin existencia.
        Assert.Equal(10_000, day15.Rows.Single(r => r.Sku == "INV-A").OnHandThousandths);
        Assert.Equal(StockStatus.Normal, day15.Rows.Single(r => r.Sku == "INV-A").Status);
        Assert.Equal(0, day25.Rows.Single(r => r.Sku == "INV-A").OnHandThousandths);
        Assert.Equal(StockStatus.Out, day25.Rows.Single(r => r.Sku == "INV-A").Status);
    }

    [Fact]
    public async Task EstadoConteosYProductosIncluidos()
    {
        using var db = await TestDb.CreateAsync();
        await SeedAsync(db);

        var day15 = await GetAsync(db, EndOf(15), new InventoryReportQuery(new DateOnly(2026, 9, 15)));

        // Incluye activos e inactivos que controlan inventario; excluye el que no controla y el creado después.
        Assert.Equal(["INV-A", "INV-B", "INV-C", "INV-F"], day15.Rows.Select(r => r.Sku));
        Assert.Equal(new InventoryCounts(4, 3, 1, 0, 3), day15.Counts);

        // Existencia igual al mínimo: baja. Sin mínimo definido: nunca baja.
        Assert.Equal(StockStatus.Low, day15.Rows.Single(r => r.Sku == "INV-B").Status);
        Assert.Equal(StockStatus.Normal, day15.Rows.Single(r => r.Sku == "INV-C").Status);
    }

    [Fact]
    public async Task ExistenciaNegativaPorVentas_EsSinExistencia()
    {
        using var db = await TestDb.CreateAsync();
        var product = await SalesTestSupport.SeedProductAsync(db, "INV-N", tracks: true);
        db.Clock.UtcNow = Utc(10);
        await SalesTestSupport.StockAsync(db, product, "3");
        await SalesTestSupport.SellOkAsync(db, (product, 5_000));

        var report = await GetAsync(db, EndOf(30), new InventoryReportQuery(new DateOnly(2026, 9, 30)));

        Assert.Equal(-2_000, report.Rows.Single().OnHandThousandths);
        Assert.Equal(StockStatus.Out, report.Rows.Single().Status);
    }

    [Fact]
    public async Task FiltroBusquedaYPaginacion_SoloAfectanALaTabla()
    {
        using var db = await TestDb.CreateAsync();
        await SeedAsync(db);
        var asOf = new DateOnly(2026, 9, 25);

        var outOnly = await GetAsync(db, EndOf(25), new InventoryReportQuery(asOf, StockFilter.Out));
        var search = await GetAsync(db, EndOf(25), new InventoryReportQuery(asOf, SearchText: "inv-b"));
        var page2 = await GetAsync(db, EndOf(25), new InventoryReportQuery(asOf, Page: 2, PageSize: 2));
        var all = await GetAsync(db, EndOf(25), new InventoryReportQuery(asOf));

        Assert.Equal(["INV-A"], outOnly.Rows.Select(r => r.Sku));
        Assert.Equal(all.Counts, outOnly.Counts);
        Assert.Equal(["INV-B"], search.Rows.Select(r => r.Sku));
        Assert.Equal(all.Counts, search.Counts);
        Assert.Equal(5, page2.TotalRows);
        Assert.Equal(2, page2.Rows.Count);
        Assert.Equal(3, page2.TotalPages);
    }

    private static async Task<InventoryReport> GetAsync(TestDb db, DateTime endUtcExclusive, InventoryReportQuery query)
    {
        await using var context = db.CreateDbContext();
        return await new InventoryReportReader(context).GetAsync(endUtcExclusive, query, TestContext.Current.CancellationToken);
    }

    private static async Task MoveAsync(TestDb db, Product product, MovementType type, string quantity, int day, string? reason = null)
    {
        db.Clock.UtcNow = Utc(day);
        await using var context = db.CreateDbContext();
        var result = await InventoryTestSupport.Handler(context)
            .HandleAsync(new RegisterMovementCommand(product.Id, type, quantity, reason, null), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    private static async Task<Product> ProductAsync(TestDb db, string sku, int createdDay, bool tracks = true, long? minimum = null)
    {
        db.Clock.UtcNow = Utc(createdDay, 8);
        return await InventoryTestSupport.SeedProductAsync(db, sku, tracks: tracks, minimumThousandths: minimum);
    }

    private static async Task SeedAsync(TestDb db)
    {
        var a = await ProductAsync(db, "INV-A", 1, minimum: 5_000);
        await MoveAsync(db, a, MovementType.Initial, "10", 2);
        await MoveAsync(db, a, MovementType.AdjustOut, "10", 20, "Merma");

        var b = await ProductAsync(db, "INV-B", 1, minimum: 5_000);
        await MoveAsync(db, b, MovementType.Initial, "5", 2);

        var c = await ProductAsync(db, "INV-C", 1);
        await MoveAsync(db, c, MovementType.Initial, "3", 2);

        await ProductAsync(db, "INV-D", 1, tracks: false);

        // Un producto se desactiva después de tener movimientos (no se puede mover uno inactivo).
        var f = await ProductAsync(db, "INV-F", 1);
        await MoveAsync(db, f, MovementType.Initial, "2", 2);
        await using (var context = db.CreateDbContext())
        {
            var inactive = context.Products.Single(p => p.Id == f.Id);
            inactive.Update(inactive.Name, inactive.Sku, null, inactive.Price, inactive.UnitCode, isActive: false, tracksInventory: true, minimumStock: null, hasMovements: true);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Creado después del día 15: no aparece en consultas anteriores.
        var e = await ProductAsync(db, "INV-E", 25);
        await MoveAsync(db, e, MovementType.Initial, "7", 25);
    }
}
