using Pos.Application.Categories;
using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Products;
using Pos.Domain.Reports;
using Pos.Domain.Returns;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.Discounts;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>
/// 016, Historia 3: "Ventas por categoría" y el reporte filtrado cuadran al centavo con el reporte sin filtro
/// (SC-002, SC-003) con descuentos de línea y global, devolución parcial, venta cancelada, productos sin
/// categoría y reclasificación (categoría vigente).
/// </summary>
public sealed class SalesByCategoryReportTests : DiscountTestBase
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly ReportPeriod Period = ReportPeriod.Custom(new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 30));

    private Guid _bebidas;
    private Guid _botanas;

    [Fact]
    public async Task VentasPorCategoria_SumanElTotalSinFiltro_YCadaCategoriaEsLaSumaDeSusProductos()
    {
        await SeedAsync();

        var report = await GetAsync(new SalesReportQuery(Period));

        // S1 5,100 + S2 1,800 + S3 (3,000 − 1,500 devuelto) + S5 800; la cancelada no cuenta.
        Assert.Equal(9_200, report.Totals.TotalCents);
        Assert.Equal(report.Totals.TotalCents, report.Categories.Sum(c => c.AmountCents));
        Assert.Equal(
            [("Bebidas", 5_300L), ("Botanas", 3_000L), (CategoryMessages.Uncategorized, 900L)],
            report.Categories.Select(c => (c.Name, c.AmountCents)));
        Assert.All(report.Categories, c =>
        {
            Assert.Equal(c.AmountCents, c.Products.Sum(p => p.AmountCents));
            Assert.Equal(c.UnitsThousandths, c.Products.Sum(p => p.UnitsThousandths));
        });
        Assert.Equal(
            [ShareMath.BasisPoints(5_300, 9_200), ShareMath.BasisPoints(3_000, 9_200), ShareMath.BasisPoints(900, 9_200)],
            report.Categories.Select(c => c.ShareBasisPoints));

        // Productos de mayor a menor unidades; la devolución también resta unidades.
        var bebidas = report.Categories[0];
        Assert.Equal(["Cola", "Agua", "Maní"], bebidas.Products.Select(p => p.Name));
        Assert.Equal(2_000, report.Categories[1].UnitsThousandths);
    }

    [Fact]
    public async Task FiltroPorCategoria_LaVentaMixtaCuentaUnaVezConSoloSusLineas_YSinDesgloseDePagos()
    {
        var (mixed, _) = await SeedAsync();

        var report = await GetAsync(new SalesReportQuery(Period, Category: CategoryFilter.Only(_bebidas)));

        Assert.Equal((3, 5_300L), (report.Totals.SalesCount, report.Totals.TotalCents));
        Assert.Equal(3, report.TotalRows);
        Assert.Equal(3_600, report.Rows.Single(r => r.SaleId == mixed).TotalCents);
        Assert.False(report.Totals.PaymentsBreakdownAvailable);
        Assert.Equal(0, report.Totals.CashCents);
        Assert.Equal(400 + 100, report.Totals.DiscountCents);
        Assert.Equal(5_300, report.Days.Sum(d => d.TotalCents));
        Assert.Equal("Bebidas", Assert.Single(report.Categories).Name);

        var uncategorized = await GetAsync(new SalesReportQuery(Period, Category: CategoryFilter.Uncategorized));
        Assert.Equal((1, 900L), (uncategorized.Totals.SalesCount, uncategorized.Totals.TotalCents));
    }

    [Fact]
    public async Task ReclasificarUnProducto_MueveSusVentasPasadasALaCategoriaVigente()
    {
        var (_, mani) = await SeedAsync();
        await CategoryTestSupport.AssignAsync(Db, mani.Id, _botanas);

        var report = await GetAsync(new SalesReportQuery(Period));

        Assert.Equal(
            [("Bebidas", 4_500L), ("Botanas", 3_800L), (CategoryMessages.Uncategorized, 900L)],
            report.Categories.Select(c => (c.Name, c.AmountCents)));
        Assert.Contains(report.Categories.Single(c => c.CategoryId == _botanas).Products, p => p.ProductId == mani.Id);
    }

    /// <summary>
    /// Bebidas: Cola $20, Agua $10, Maní $8 (vendido en Botanas y reclasificado a Bebidas). Botanas: Papas
    /// $15. Sin categoría: Chicle $5. Devuelve la venta mixta y el maní.
    /// </summary>
    private async Task<(Guid MixedSaleId, Product Mani)> SeedAsync()
    {
        _bebidas = await CategoryTestSupport.AddCategoryAsync(Db, "Bebidas");
        _botanas = await CategoryTestSupport.AddCategoryAsync(Db, "Botanas");
        var cola = await CategorizedAsync("Cola", 2_000, _bebidas);
        var agua = await CategorizedAsync("Agua", 1_000, _bebidas);
        var papas = await CategorizedAsync("Papas", 1_500, _botanas);
        var chicle = await CategorizedAsync("Chicle", 500, null);
        var mani = await CategorizedAsync("Maní", 800, _botanas);

        // S1 mixta: 2 colas con 10 % de descuento de línea (3,600) + 1 papas (1,500).
        var mixed = await SellOkAsync([Line(cola, 2_000, Percent(1_000)), Line(papas, 1_000)], 5_100);

        // S2: agua + 2 chicles con $2.00 de descuento global repartido 100 / 100.
        await SellOkAsync([Line(agua, 1_000), Line(chicle, 2_000)], 1_800, OrderDiscountInput.Manual(DiscountMode.Amount, 200));

        // S3: 2 papas con devolución parcial de 1.
        var s3 = await SellOkAsync([Line(papas, 2_000)], 3_000);
        var returned = await Discounts.Returns.ReturnAsync(s3, [new ReturnLineRequest(await Discounts.Returns.LineIdAsync(s3, 1), 1_000)]);
        Assert.True(returned.IsSuccess, returned.Error?.ToString());

        // S4 cancelada.
        var s4 = await SellOkAsync([Line(cola, 1_000)], 2_000);
        Assert.True((await Discounts.Returns.CancelAsync(s4)).IsSuccess);

        // S5: maní vendido en Botanas y después reclasificado a Bebidas.
        await SellOkAsync([Line(mani, 1_000)], 800);
        await CategoryTestSupport.AssignAsync(Db, mani.Id, _bebidas);
        return (mixed, mani);
    }

    private async Task<Product> CategorizedAsync(string name, long priceCents, Guid? categoryId)
    {
        var product = await ProductAsync(name.ToUpperInvariant(), priceCents);
        await CategoryTestSupport.AssignAsync(Db, product.Id, categoryId);
        await using var context = Db.CreateDbContext();
        var renamed = context.Products.Single(p => p.Id == product.Id);
        renamed.Update(name, renamed.Sku, null, renamed.Price, renamed.UnitCode, true, categoryId: categoryId);
        await context.SaveChangesAsync(Ct);
        return renamed;
    }

    private async Task<Guid> SellOkAsync(IReadOnlyList<ConfirmLineInput> lines, long totalCents, OrderDiscountInput? orderDiscount = null)
    {
        var result = await Discounts.SellAsync(new ConfirmSaleCommand(Guid.CreateVersion7(), lines, DiscountTestSupport.Cash(totalCents), OrderDiscount: orderDiscount));
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.SaleId;
    }

    private async Task<SalesReport> GetAsync(SalesReportQuery query)
    {
        await using var context = Db.CreateDbContext();
        var result = await new GetSalesReportHandler(new AllowAllAccessControl(), new SalesReportReader(context), Resolver).HandleAsync(query, Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }
}
