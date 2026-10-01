using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Reports;

public class GetInventoryReportHandlerTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly InventoryReportQuery Query = new(new DateOnly(2026, 9, 15));

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Cashier)]
    public async Task AdministradorYCajero_ConsultanElInventario(UserRole role)
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("usuario", role));
        var reader = new FakeReader();
        var handler = new GetInventoryReportHandler(fixture.Access, reader, Resolver);

        var result = await handler.HandleAsync(Query, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        // El límite es el inicio del día siguiente al de la fecha consultada.
        Assert.Equal(new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc), reader.LastEnd);
    }

    [Fact]
    public async Task SinSesion_EsRechazadoYFechaInvalidaDevuelveError()
    {
        var fixture = new AuthFixture();
        var reader = new FakeReader();
        var handler = new GetInventoryReportHandler(fixture.Access, reader, Resolver);

        Assert.IsType<Forbidden>((await handler.HandleAsync(Query, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(0, reader.Calls);

        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var invalid = await handler.HandleAsync(Query with { AsOfDate = default }, TestContext.Current.CancellationToken);
        Assert.IsType<ValidationFailed>(invalid.Error);
    }

    private sealed class FakeReader : IInventoryReportReader
    {
        public int Calls { get; private set; }

        public DateTime LastEnd { get; private set; }

        public Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            LastEnd = endUtcExclusive;
            return Task.FromResult(new InventoryReport(new InventoryCounts(0, 0, 0, 0, 0), [], 0, 1, 100));
        }
    }
}
