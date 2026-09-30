using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.TestSupport;

/// <summary>
/// Repositorio de inventario en memoria para probar casos de uso que solo necesitan saber si un
/// producto tiene movimientos. Las búsquedas devuelven páginas vacías: su lógica se prueba con
/// SQLite real en Pos.Infrastructure.Tests.
/// </summary>
public sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly Dictionary<Guid, ProductStock> _stocks = [];

    /// <summary>Simula que el producto ya tiene un movimiento (inventario inicial de 1).</summary>
    public void SeedMovement(Guid productId)
    {
        var stock = ProductStock.Start(productId);
        stock.Record(MovementType.Receipt, Pos.Domain.Common.Quantity.FromThousandths(1000), Pos.Domain.Products.UnitOfMeasure.Piece, true, true, null, null);
        _stocks[productId] = stock;
    }

    public Task<ProductStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_stocks.GetValueOrDefault(productId));

    public Task<bool> HasMovementsAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_stocks.ContainsKey(productId));

    public void AddStock(ProductStock stock) => _stocks[stock.ProductId] = stock;

    public void AddMovement(InventoryMovement movement)
    {
    }

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SaveOutcome.Saved);

    public Task<StockPage> SearchStockAsync(StockSearch search, CancellationToken cancellationToken) =>
        Task.FromResult(new StockPage([], 0, 1, StockPage.DefaultPageSize));

    public Task<StockAlertCounts> CountAlertsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new StockAlertCounts(0, 0));

    public Task<MovementPage> SearchMovementsAsync(MovementSearch search, CancellationToken cancellationToken) =>
        Task.FromResult(new MovementPage([], 0, 1, MovementPage.DefaultPageSize));
}
