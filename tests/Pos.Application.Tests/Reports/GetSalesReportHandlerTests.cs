using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Reports;

public class GetSalesReportHandlerTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);

    [Fact]
    public async Task Cajero_EsRechazadoYNoSeInvocaElLector()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("cajero", UserRole.Cashier));
        var reader = new FakeSalesReader();
        var handler = new GetSalesReportHandler(fixture.Access, reader, Resolver);

        var result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Administrador_ConsultaYLaVariacionSeCalculaEnElCasoDeUso()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var reader = new FakeSalesReader
        {
            Report = Report(current: 1_125, previous: 1_000),
        };
        var handler = new GetSalesReportHandler(fixture.Access, reader, Resolver);

        var result = await handler.HandleAsync(Query(compare: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1_250, result.Value.Comparison!.VariationBasisPoints);
        Assert.NotNull(reader.LastWindow!.Previous);
        Assert.Equal(7, reader.LastWindow.Days.Count);
    }

    [Fact]
    public async Task PeriodoAnteriorEnCero_LaVariacionNoEsCalculable()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var reader = new FakeSalesReader { Report = Report(current: 500, previous: 0) };
        var handler = new GetSalesReportHandler(fixture.Access, reader, Resolver);

        var result = await handler.HandleAsync(Query(compare: true), TestContext.Current.CancellationToken);

        Assert.Null(result.Value.Comparison!.VariationBasisPoints);
    }

    private static SalesReportQuery Query(bool compare = false) =>
        new(ReportPeriod.Last7Days(new DateOnly(2026, 9, 30)), Compare: compare);

    private static SalesReport Report(long current, long previous) =>
        new(
            new SalesTotals(1, current, current, current, 0, 0),
            new SalesComparison(new SalesTotals(1, previous, previous, previous, 0, 0), null),
            [],
            [],
            0,
            1,
            100,
            []);

    private sealed class FakeSalesReader : ISalesReportReader
    {
        public int Calls { get; private set; }

        public SalesReportWindow? LastWindow { get; private set; }

        public SalesReport Report { get; init; } = new(SalesTotals.Empty, null, [], [], 0, 1, 100, []);

        public Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            LastWindow = window;
            return Task.FromResult(Report);
        }
    }
}
