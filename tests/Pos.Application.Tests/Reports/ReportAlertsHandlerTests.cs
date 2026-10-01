using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.GetReportAlerts;
using Pos.Application.Reports.SetProductCritical;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Reports;

public class ReportAlertsHandlerTests
{
    [Fact]
    public async Task Cajero_NoVeLasAlertas()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("cajero", UserRole.Cashier));
        var handler = new GetReportAlertsHandler(
            fixture.Access, new EmptyCashReader(), new EmptyProducts(), new Settings(), new ReportPeriodResolver(TimeZoneInfo.Utc), fixture.Clock);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.IsType<Forbidden>(result.Error);
    }

    [Fact]
    public async Task SinAlertas_EstaVacio()
    {
        var fixture = new AuthFixture();
        fixture.SignedIn(fixture.AddUser("admin", UserRole.Admin));
        var handler = new GetReportAlertsHandler(
            fixture.Access, new EmptyCashReader(), new EmptyProducts(), new Settings(), new ReportPeriodResolver(TimeZoneInfo.Utc), fixture.Clock);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public async Task MarcarProductoCritico_SoloConManageProducts_YProductoInexistenteDaError()
    {
        var products = new InMemoryProductRepository();
        var product = products.Seed(Product.Create("Pan", "PAN-1", null, Money.FromCents(1000), "H87", tracksInventory: true));

        var cashier = new AuthFixture();
        cashier.SignedIn(cashier.AddUser("cajero", UserRole.Cashier));
        var denied = await new SetProductCriticalHandler(cashier.Access, products)
            .HandleAsync(new SetProductCriticalCommand(product.Id, true), TestContext.Current.CancellationToken);
        Assert.IsType<Forbidden>(denied.Error);
        Assert.False(products.All.Single().IsCritical);

        var admin = new AuthFixture();
        admin.SignedIn(admin.AddUser("admin", UserRole.Admin));
        var handler = new SetProductCriticalHandler(admin.Access, products);
        Assert.True((await handler.HandleAsync(new SetProductCriticalCommand(product.Id, true), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True(products.All.Single().IsCritical);
        Assert.IsType<NotFound>((await handler.HandleAsync(new SetProductCriticalCommand(Guid.NewGuid(), true), TestContext.Current.CancellationToken)).Error);
    }

    private sealed class EmptyCashReader : ICashCountReportReader
    {
        public Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CashCountRawRow>>([]);
    }

    private sealed class EmptyProducts : IReportAlertsReader
    {
        public Task<IReadOnlyList<CriticalProductRow>> ListCriticalProductsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CriticalProductRow>>([]);
    }

    private sealed class Settings : IReportSettingsStore
    {
        public ReportSettings Load() => new();

        public void Save(ReportSettings settings)
        {
        }
    }
}
