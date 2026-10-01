using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Domain.Reports;
using Pos.Domain.Sales;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

public sealed class SalesReportReaderTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly ReportPeriod Period = ReportPeriod.Custom(new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task ExcluyeCanceladas_LosPagosSumanElTotalYElPromedioEsTotalEntreVentas()
    {
        using var db = await TestDb.CreateAsync();
        var (ana, luis) = await SeedAsync(db);

        var report = await GetAsync(db, new SalesReportQuery(Period));

        Assert.Equal(new SalesTotals(3, 6_000, 2_000, 1_000, 2_000, 3_000), report.Totals);
        Assert.Equal(report.Totals.TotalCents, report.Totals.CashCents + report.Totals.CardCents + report.Totals.TransferCents);
        Assert.Equal(3, report.TotalRows);
        Assert.DoesNotContain(report.Rows, r => r.TotalCents == 5_000);
        Assert.Equal([0L, 1_000, 5_000, 0], report.Days.Select(d => d.TotalCents));
        Assert.Equal(4, report.Days.Count);
        Assert.NotEqual(ana.Id, luis.Id);
    }

    [Fact]
    public async Task FiltroPorCajero_SoloConsideraSusVentas()
    {
        using var db = await TestDb.CreateAsync();
        var (ana, _) = await SeedAsync(db);

        var report = await GetAsync(db, new SalesReportQuery(Period, CashierId: ana.Id));

        Assert.Equal(2, report.Totals.SalesCount);
        Assert.Equal(3_000, report.Totals.TotalCents);
        Assert.All(report.Rows, r => Assert.Equal(ana.FullName, r.CashierName));
    }

    [Fact]
    public async Task Comparativo_ConPeriodoAnteriorSinVentas_Trae_TotalesEnCero()
    {
        using var db = await TestDb.CreateAsync();
        await SeedAsync(db);

        var report = await GetAsync(db, new SalesReportQuery(Period, Compare: true));

        Assert.NotNull(report.Comparison);
        Assert.Equal(SalesTotals.Empty, report.Comparison.Previous);
    }

    [Fact]
    public async Task TablaOrdenadaYPaginada()
    {
        using var db = await TestDb.CreateAsync();
        await SeedAsync(db);

        var byTotal = await GetAsync(db, new SalesReportQuery(Period, Sort: SalesReportSort.Total, Descending: true, Page: 2, PageSize: 2));

        Assert.Equal(3, byTotal.TotalRows);
        Assert.Equal([1_000L], byTotal.Rows.Select(r => r.TotalCents));
        Assert.Equal(2, byTotal.TotalPages);
    }

    private static async Task<SalesReport> GetAsync(TestDb db, SalesReportQuery query)
    {
        await using var context = db.CreateDbContext();
        var window = new SalesReportWindow(
            Resolver.Resolve(query.Period),
            Resolver.Days(query.Period),
            query.Compare ? Resolver.Resolve(query.Period.Previous()) : null);
        return await new SalesReportReader(context).GetAsync(window, query, TestContext.Current.CancellationToken);
    }

    private static async Task<(Pos.Domain.Users.User Ana, Pos.Domain.Users.User Luis)> SeedAsync(TestDb db)
    {
        var ana = await ReportTestSupport.AddUserAsync(db, "ana");
        var luis = await ReportTestSupport.AddUserAsync(db, "luis");
        var product = await ReportTestSupport.SeedProductAsync(db);

        await ReportTestSupport.SellAsync(db, ana, new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc), product, 1_000, PaymentMethod.Cash);
        await ReportTestSupport.SellAsync(db, ana, new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc), product, 2_000, PaymentMethod.Card);
        await ReportTestSupport.SellAsync(db, luis, new DateTime(2026, 9, 29, 11, 0, 0, DateTimeKind.Utc), product, 3_000, PaymentMethod.Transfer);
        var cancelled = await ReportTestSupport.SellAsync(db, luis, new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc), product, 5_000, PaymentMethod.Cash);
        Assert.True((await SalesTestSupport.CancelAsync(db, cancelled.SaleId)).IsSuccess);
        return (ana, luis);
    }
}
