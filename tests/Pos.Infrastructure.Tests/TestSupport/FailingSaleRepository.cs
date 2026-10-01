using Pos.Application.CashShifts;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Repositorio de ventas que guarda y luego falla, antes de que el caso de uso confirme la
/// transacción: simula un error inesperado a medio camino (atomicidad, SC-008).
/// </summary>
public sealed class FailingSaleRepository : ISaleRepository
{
    private readonly ISaleRepository _inner;

    public FailingSaleRepository(ISaleRepository inner) => _inner = inner;

    public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken) => _inner.ExistsForDraftAsync(draftId, cancellationToken);

    public Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken) => _inner.FindByDraftAsync(draftId, cancellationToken);

    public Task<long> NextFolioNumberAsync(CancellationToken cancellationToken) => _inner.NextFolioNumberAsync(cancellationToken);

    public Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken) => _inner.GetAsync(id, cancellationToken);

    public void Add(Sale sale) => _inner.Add(sale);

    public Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken) => _inner.SearchAsync(search, cancellationToken);

    public Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken) => _inner.GetDetailAsync(id, cancellationToken);

    public Task<ShiftSalesTotals> GetShiftTotalsAsync(Guid shiftId, CancellationToken cancellationToken) => _inner.GetShiftTotalsAsync(shiftId, cancellationToken);

    public Task<IReadOnlyList<ShiftSaleRowDto>> ListByShiftAsync(Guid shiftId, CancellationToken cancellationToken) => _inner.ListByShiftAsync(shiftId, cancellationToken);

    public Task<long> GetCashAppliedAsync(Guid saleId, CancellationToken cancellationToken) => _inner.GetCashAppliedAsync(saleId, cancellationToken);

    public Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken) => _inner.GetDashboardAsync(days, cancellationToken);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _inner.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("Falla forzada antes de confirmar.");
    }
}
