using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Reports;

public class GetCashCountReportHandlerTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly CashCountReportQuery Query = new(ReportPeriod.Today(new DateOnly(2026, 9, 30)));

    [Fact]
    public async Task Cajero_EsRechazadoYNoSeInvocaElLector()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("cajero", UserRole.Cashier));
        var reader = new FakeReader([]);
        var handler = new GetCashCountReportHandler(fixture.Access, reader, Resolver, new FakeSettings());

        var result = await handler.HandleAsync(Query, TestContext.Current.CancellationToken);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task FaltanteDeSeisPorCiento_EsAlertaYElTurnoAbiertoNoTraeCifrasNiCuentaEnLosTotales()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var reader = new FakeReader(
        [
            Row(isOpen: false, expected: 100_000, counted: 94_000, difference: -6_000, sold: 10_000),
            Row(isOpen: false, expected: 100_000, counted: 102_000, difference: 2_000, sold: 5_000),

            // Un lector que por error trajera cifras de un turno abierto: el caso de uso las descarta.
            Row(isOpen: true, expected: 1, counted: 2, difference: 3, sold: 700),
        ]);
        var handler = new GetCashCountReportHandler(fixture.Access, reader, Resolver, new FakeSettings());

        var report = (await handler.HandleAsync(Query, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(-600, report.Rows[0].DifferenceBasisPoints);
        Assert.True(report.Rows[0].IsAlert);
        Assert.False(report.Rows[1].IsAlert);
        var open = report.Rows[2];
        Assert.True(open.IsOpen);
        Assert.Null(open.ExpectedCashCents);
        Assert.Null(open.CountedCashCents);
        Assert.Null(open.DifferenceCents);
        Assert.Null(open.DifferenceBasisPoints);
        Assert.False(open.IsAlert);
        Assert.Equal(new CashCountTotals(2, 15_700, -4_000), report.Totals);
    }

    [Fact]
    public async Task UmbralMayor_QuitaLaAlertaYEsperadoCeroNoEsCalculable()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var reader = new FakeReader(
        [
            Row(isOpen: false, expected: 100_000, counted: 94_000, difference: -6_000, sold: 0),
            Row(isOpen: false, expected: 0, counted: 500, difference: 500, sold: 0),
        ]);
        var settings = new FakeSettings { Current = new ReportSettings { CashDifferenceAlertBasisPoints = 1_000 } };
        var handler = new GetCashCountReportHandler(fixture.Access, reader, Resolver, settings);

        var report = (await handler.HandleAsync(Query, TestContext.Current.CancellationToken)).Value;

        Assert.False(report.Rows[0].IsAlert);
        Assert.Null(report.Rows[1].DifferenceBasisPoints);
        Assert.False(report.Rows[1].IsAlert);
        Assert.Equal(1_000, report.ThresholdBasisPoints);
    }

    private static CashCountRawRow Row(bool isOpen, long? expected, long? counted, long? difference, long sold) =>
        new(Guid.NewGuid(), "T-000001", "Ana", DateTime.UtcNow, isOpen ? null : DateTime.UtcNow, 0, sold, 0, 0, isOpen, expected, counted, difference);

    private sealed class FakeReader(IReadOnlyList<CashCountRawRow> rows) : ICashCountReportReader
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(rows);
        }
    }

    private sealed class FakeSettings : IReportSettingsStore
    {
        public ReportSettings Current { get; set; } = new();

        public ReportSettings Load() => Current;

        public void Save(ReportSettings settings) => Current = settings;
    }
}
