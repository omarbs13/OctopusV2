using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Domain.Sales;

namespace Pos.Desktop.Tests.TestSupport;

/// <summary>
/// Repositorio de ventas para los ViewModels de Inicio: solo responde el tablero. Las consultas
/// reales se prueban con SQLite en Pos.Infrastructure.Tests.
/// </summary>
public sealed class FakeSaleRepository : ISaleRepository
{
    /// <summary>Tablero que devolverá <see cref="GetDashboardAsync"/>; si es nulo, uno sin ventas.</summary>
    public SalesDashboard? Dashboard { get; set; }

    /// <summary>Ventanas recibidas en la última consulta del tablero.</summary>
    public IReadOnlyList<DayWindow>? LastDays { get; private set; }

    public int DashboardCalls { get; private set; }

    public Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken)
    {
        LastDays = days;
        DashboardCalls++;
        return Task.FromResult(Dashboard ?? new SalesDashboard(
            [.. days.Select(d => new DayTotal(d.LocalDate, 0, 0))],
            []));
    }

    public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<long> NextFolioNumberAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void Add(Sale sale) => throw new NotSupportedException();

    public Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ShiftSalesTotals> GetShiftTotalsAsync(Guid shiftId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ShiftSaleRowDto>> ListByShiftAsync(Guid shiftId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<long> GetCashAppliedAsync(Guid saleId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
