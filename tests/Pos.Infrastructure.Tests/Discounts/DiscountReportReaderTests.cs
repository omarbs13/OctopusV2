using Microsoft.EntityFrameworkCore;
using Pos.Application.Discounts;
using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Reports;
using Pos.Domain.Sales;
using Pos.Infrastructure.Discounts;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>015, FR-018 y SC-003: el total descontado del reporte coincide con la suma de las ventas del período.</summary>
public sealed class DiscountReportReaderTests : DiscountTestBase
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);

    private static ReportPeriod Today(TestDb db) =>
        ReportPeriod.Custom(DateOnly.FromDateTime(db.Clock.UtcNow), DateOnly.FromDateTime(db.Clock.UtcNow));

    private async Task SeedAsync()
    {
        var a = await ProductAsync("REP-A", 5_000);
        var c = await ProductAsync("REP-C", 10_000);

        // Cajero: línea 10 % ($10.00) y venta 10 % ($19.00) = $29.00.
        var first = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [Line(a, 2000, Percent(1000)), Line(c, 1000)],
            DiscountTestSupport.Cash(17_100),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Percent, 1000)));
        Assert.True(first.IsSuccess, first.Error?.ToString());

        // Cajero: venta cancelada con $5.00 de descuento; no cuenta.
        var cancelled = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(), [Line(a, 1000, Amount(500))], DiscountTestSupport.Cash(4_500)));
        Assert.True(cancelled.IsSuccess, cancelled.Error?.ToString());
        Assert.True((await Discounts.CancelAsync(cancelled.Value.SaleId)).IsSuccess);

        // Administrador: línea de $3.00.
        Users.As(Users.Admin);
        var admin = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(), [Line(c, 1000, Amount(300))], DiscountTestSupport.Cash(9_700)));
        Assert.True(admin.IsSuccess, admin.Error?.ToString());
    }

    private async Task<DiscountReport> ReadAsync(DiscountReportQuery query)
    {
        await using var context = Db.CreateDbContext();
        return await new DiscountReportReader(context).ReadAsync(query, Resolver.Resolve(query.Period), Ct);
    }

    private async Task<long> SumOfCompletedSalesAsync()
    {
        await using var context = Db.CreateDbContext();
        return await context.Sales.Where(s => s.Status == SaleStatus.Completed).SumAsync(s => s.DiscountCents, Ct);
    }

    [Fact]
    public async Task TotalDescontado_IgualALaSumaDeLasVentasCompletadas_YFiltraPorCajeroYTipo()
    {
        await SeedAsync();
        var period = Today(Db);

        var all = await ReadAsync(new DiscountReportQuery(period, null, null));
        Assert.Equal(3_200, all.TotalDiscountCents);
        Assert.Equal(await SumOfCompletedSalesAsync(), all.TotalDiscountCents);
        Assert.Equal(3, all.Count);
        Assert.Equal(all.TotalDiscountCents, all.Rows.Sum(r => r.AmountCents));

        var cashier = await ReadAsync(new DiscountReportQuery(period, Users.Cashier.Id, null));
        Assert.Equal(2_900, cashier.TotalDiscountCents);

        var orders = await ReadAsync(new DiscountReportQuery(period, null, DiscountKind.Order));
        Assert.Equal(1_900, Assert.Single(orders.Rows).AmountCents);
    }

    [Fact]
    public async Task ReporteDeVentas_IncluyeElTotalDescontadoDelPeriodo()
    {
        await SeedAsync();
        var period = Today(Db);

        await using var context = Db.CreateDbContext();
        var report = await new SalesReportReader(context).GetAsync(
            new SalesReportWindow(Resolver.Resolve(period), Resolver.Days(period), null),
            new SalesReportQuery(period),
            Ct);

        Assert.Equal(3_200, report.Totals.DiscountCents);
    }
}
