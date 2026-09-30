using Pos.Application.Products;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

/// <summary>Persistencia del agregado Venta. Específico del agregado; no hay repositorios genéricos.</summary>
public interface ISaleRepository
{
    Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken);

    /// <summary>La venta ya registrada para el borrador, o nula.</summary>
    Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken);

    /// <summary><c>MAX(FolioNumber) + 1</c>; se llama dentro de la transacción de escritura (research §4).</summary>
    Task<long> NextFolioNumberAsync(CancellationToken cancellationToken);

    /// <summary>Venta con líneas y pagos, con seguimiento de cambios.</summary>
    Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(Sale sale);

    Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken);

    Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Conflict"/> por concurrencia;
    /// <see cref="SaveStatus.Duplicate"/> con <c>DraftId</c> o <c>Folio</c> si se viola su índice único.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
