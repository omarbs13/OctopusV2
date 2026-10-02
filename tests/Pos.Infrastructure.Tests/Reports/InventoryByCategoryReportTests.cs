using Pos.Application.Categories;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>016, Historia 3, escenario 8: el filtro de categoría limita tarjetas y tabla del inventario.</summary>
public sealed class InventoryByCategoryReportTests
{
    private static readonly DateTime End = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FiltroPorCategoriaOSinCategoria_TarjetasYTablaSoloDeEsosProductos_YOrdenPorCategoria()
    {
        using var db = await TestDb.CreateAsync();
        var lacteos = await CategoryTestSupport.AddCategoryAsync(db, "Lácteos", active: false);
        var bebidas = await CategoryTestSupport.AddCategoryAsync(db, "Bebidas");
        await TrackedAsync(db, "LECHE", "10", lacteos);
        await TrackedAsync(db, "QUESO", null, lacteos);
        await TrackedAsync(db, "COLA", "3", bebidas);
        await TrackedAsync(db, "PAN", "2", null);

        var onlyLacteos = await GetAsync(db, CategoryFilter.Only(lacteos));
        Assert.Equal((2, 1), (onlyLacteos.Counts.Total, onlyLacteos.Counts.Out));
        Assert.All(onlyLacteos.Rows, r => Assert.Equal(("Lácteos", false), (r.CategoryName, r.CategoryIsActive)));

        var uncategorized = await GetAsync(db, CategoryFilter.Uncategorized);
        var pan = Assert.Single(uncategorized.Rows);
        Assert.Equal(("PAN", (string?)null), (pan.Sku, pan.CategoryName));
        Assert.Equal(1, uncategorized.Counts.Total);

        var all = await GetAsync(db, CategoryFilter.All, InventoryReportSort.Category);
        Assert.Equal(4, all.Counts.Total);
        Assert.Equal(["COLA", "LECHE", "QUESO", "PAN"], all.Rows.Select(r => r.Sku));
    }

    private static async Task TrackedAsync(TestDb db, string sku, string? stock, Guid? categoryId)
    {
        db.Clock.UtcNow = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        var product = await SalesTestSupport.SeedProductAsync(db, sku, tracks: true);
        await CategoryTestSupport.AssignAsync(db, product.Id, categoryId);
        if (stock is not null)
        {
            await SalesTestSupport.StockAsync(db, product, stock);
        }
    }

    private static async Task<InventoryReport> GetAsync(TestDb db, CategoryFilter category, InventoryReportSort sort = InventoryReportSort.Name)
    {
        await using var context = db.CreateDbContext();
        return await new InventoryReportReader(context).GetAsync(
            End,
            new InventoryReportQuery(new DateOnly(2026, 9, 29), Sort: sort, Category: category),
            TestContext.Current.CancellationToken);
    }
}
