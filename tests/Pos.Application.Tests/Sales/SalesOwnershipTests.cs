using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSale;
using Pos.Application.Sales.SearchSales;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Sales;

public class SalesOwnershipTests
{
    private readonly AuthFixture _auth = new();
    private readonly FakeSales _sales = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SaleDetailDto Detail(Guid createdBy) =>
        new(Guid.NewGuid(), "V-000001", DateTime.UtcNow, "x", 100, SaleStatus.Completed, 1, null, null, null, [], [], createdBy);

    [Fact]
    public async Task ElCajero_ConsultaSuPropiaVenta_YRecibeForbiddenConLaDeOtro()
    {
        var cashier = _auth.AddUser("caja", UserRole.Cashier);
        var other = _auth.AddUser("otra", UserRole.Cashier);
        _auth.SignedIn(cashier);
        var handler = new GetSaleHandler(_auth.Access, _auth.Session, _sales);

        _sales.Detail = Detail(cashier.Id);
        var own = await handler.HandleAsync(new GetSaleQuery(_sales.Detail.Id), Ct);
        _sales.Detail = Detail(other.Id);
        var foreign = await handler.HandleAsync(new GetSaleQuery(_sales.Detail.Id), Ct);

        Assert.True(own.IsSuccess);
        Assert.IsType<Forbidden>(foreign.Error);
    }

    [Fact]
    public async Task ElAdministrador_VeLasVentasDeTodos()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var cashier = _auth.AddUser("caja", UserRole.Cashier);
        _auth.SignedIn(admin);
        _sales.Detail = Detail(cashier.Id);

        var result = await new GetSaleHandler(_auth.Access, _auth.Session, _sales).HandleAsync(new GetSaleQuery(_sales.Detail.Id), Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SearchSales_ParaElCajeroFuerzaElCajeroActual_YRechazaPedirOtro()
    {
        var cashier = _auth.AddUser("caja", UserRole.Cashier);
        var other = _auth.AddUser("otra", UserRole.Cashier);
        _auth.SignedIn(cashier);
        var handler = new SearchSalesHandler(_auth.Access, _auth.Session, _sales);

        var own = await handler.HandleAsync(new SearchSalesQuery(null, null, null, null), Ct);
        var foreign = await handler.HandleAsync(new SearchSalesQuery(null, null, null, null, CashierId: other.Id), Ct);

        Assert.True(own.IsSuccess);
        Assert.Equal(cashier.Id, _sales.LastSearch!.CashierId);
        Assert.IsType<Forbidden>(foreign.Error);
    }

    [Fact]
    public async Task SearchSales_ParaElAdministradorElFiltroEsOpcional()
    {
        var admin = _auth.AddUser("admin", UserRole.Admin);
        var cashier = _auth.AddUser("caja", UserRole.Cashier);
        _auth.SignedIn(admin);
        var handler = new SearchSalesHandler(_auth.Access, _auth.Session, _sales);

        await handler.HandleAsync(new SearchSalesQuery(null, null, null, null), Ct);
        Assert.Null(_sales.LastSearch!.CashierId);

        await handler.HandleAsync(new SearchSalesQuery(null, null, null, null, CashierId: cashier.Id), Ct);
        Assert.Equal(cashier.Id, _sales.LastSearch!.CashierId);
    }

    private sealed class FakeSales : ISaleRepository
    {
        public SaleDetailDto? Detail { get; set; }

        public SaleSearch? LastSearch { get; private set; }

        public Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Detail);

        public Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken)
        {
            LastSearch = search;
            return Task.FromResult(new SalePage([], 0, 1, search.PageSize));
        }

        public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> NextFolioNumberAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Sale sale) => throw new NotSupportedException();

        public Task<ShiftSalesTotals> GetShiftTotalsAsync(Guid shiftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ShiftSaleRowDto>> ListByShiftAsync(Guid shiftId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> GetCashAppliedAsync(Guid saleId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Pos.Application.Products.SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
