using Pos.Application.Products;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

/// <summary>
/// Persistencia de existencias y movimientos. No ofrece <c>Update</c> ni <c>Remove</c> de
/// movimientos: son inmutables (FR-010).
/// </summary>
public interface IInventoryRepository
{
    /// <summary>Existencia con seguimiento de cambios, o nula si el producto no tiene movimientos.</summary>
    Task<ProductStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Existencias con seguimiento de cambios de los productos indicados; sin fila, no aparecen.</summary>
    Task<IReadOnlyDictionary<Guid, ProductStock>> GetStocksAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    /// <summary>Indica si el producto tiene movimientos (existe su fila de existencia).</summary>
    Task<bool> HasMovementsAsync(Guid productId, CancellationToken cancellationToken);

    void AddStock(ProductStock stock);

    void AddMovement(InventoryMovement movement);

    /// <summary>
    /// Guarda la existencia y el movimiento. <see cref="SaveOutcome.Conflict"/> si falla la
    /// concurrencia o el índice único de la secuencia.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Existencias paginadas, ordenadas por nombre y SKU.</summary>
    Task<StockPage> SearchStockAsync(StockSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Conteos de las tarjetas de Inicio: solo productos activos, no borrados y que controlan
    /// inventario. Usa el mismo predicado que <see cref="SearchStockAsync"/>.
    /// </summary>
    Task<StockAlertCounts> CountAlertsAsync(CancellationToken cancellationToken);

    /// <summary>Historial paginado, del más reciente al más antiguo.</summary>
    Task<MovementPage> SearchMovementsAsync(MovementSearch search, CancellationToken cancellationToken);
}

public enum StockFilter
{
    All,
    Normal,
    Low,
    Out,
}

/// <summary>Criterios de existencias ya normalizados por el caso de uso.</summary>
public sealed record StockSearch(
    string? NameText,
    string? SkuText,
    string? BarcodeText,
    bool BarcodeExact,
    StockFilter Filter,
    bool IncludeInactive,
    int Page,
    int PageSize);

public sealed record StockItemDto(
    Guid ProductId,
    string Name,
    string Sku,
    string? Barcode,
    string UnitCode,
    string UnitName,
    int DecimalPlaces,
    long OnHandThousandths,
    long? MinimumThousandths,
    StockStatus Status,
    bool IsActive,
    bool HasMovements = false,
    bool IsCritical = false);

public sealed record StockPage(IReadOnlyList<StockItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => ProductPage.PageCount(TotalCount, PageSize);
}

public sealed record StockAlertCounts(long Low, long Out);

/// <summary>Criterios del historial; el límite superior de fecha es exclusivo.</summary>
public sealed record MovementSearch(
    Guid? ProductId,
    MovementType? Type,
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    int Page,
    int PageSize);

public sealed record MovementDto(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string UnitName,
    int DecimalPlaces,
    MovementType Type,
    long QuantityThousandths,
    long ResultingStockThousandths,
    string? Reason,
    string? Reference,
    Guid CreatedBy,
    string CreatedByName);

public sealed record MovementPage(IReadOnlyList<MovementDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => ProductPage.PageCount(TotalCount, PageSize);
}
