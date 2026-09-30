using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>
/// Prueba de consistencia de ventas (obligatoria, Principio VI): tras una secuencia reproducible de
/// ventas (algunas sin existencia suficiente), cancelaciones, entradas y ajustes, la existencia de
/// cada producto es la suma con signo de sus movimientos y se cumplen los invariantes de data-model.md.
/// </summary>
public sealed class SaleConsistencyTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private readonly List<Product> _products = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-P1", "H87", true, 1050));
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-P2", "H87", true, 999));
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-P3", "XBX", true, 2500));
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-K1", "KGM", true, 1005));
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-K2", "KGM", true, 8500));
        _products.Add(await SalesTestSupport.SeedProductAsync(_db, "CON-S1", "H87", false, 5000));
        foreach (var product in _products.Where(p => p.TracksInventory))
        {
            await SalesTestSupport.StockAsync(_db, product, product.UnitCode == "KGM" ? "5.000" : "8");
        }
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private static long RandomQuantity(Random random, Product product) =>
        UnitOfMeasure.Find(product.UnitCode)!.DecimalPlaces == 3
            ? random.Next(1, 3500)
            : random.Next(1, 7) * 1000L;

    [Fact]
    public async Task Random_sequence_of_sales_cancellations_and_movements_keeps_every_invariant()
    {
        var random = new Random(20260930);
        var completed = new List<Guid>();
        var cancelled = new List<Guid>();

        for (var i = 0; i < 250; i++)
        {
            switch (random.Next(10))
            {
                case <= 5:
                    var lines = _products.OrderBy(_ => random.Next()).Take(random.Next(1, 4))
                        .Select(p => (p, RandomQuantity(random, p))).ToArray();
                    var sale = await SalesTestSupport.SellAsync(_db, SalesTestSupport.CashSale(Guid.CreateVersion7(), lines));
                    Assert.True(sale.IsSuccess, sale.Error?.ToString());
                    completed.Add(sale.Value.SaleId);
                    break;

                case 6 when completed.Count > 0:
                    var id = completed[random.Next(completed.Count)];
                    completed.Remove(id);
                    Assert.True((await SalesTestSupport.CancelAsync(_db, id)).IsSuccess);
                    cancelled.Add(id);
                    break;

                case 7:
                    var tracked = _products.Where(p => p.TracksInventory).ToList();
                    await SalesTestSupport.StockAsync(_db, tracked[random.Next(tracked.Count)], "1", MovementType.Receipt);
                    break;

                case 8:
                    var target = _products.Where(p => p.TracksInventory).ToList()[random.Next(5)];
                    await using (var context = _db.CreateDbContext())
                    {
                        var result = await InventoryTestSupport.Handler(context)
                            .HandleAsync(new RegisterMovementCommand(target.Id, MovementType.AdjustOut, "1", "merma", null), Ct);
                        Assert.True(result.IsSuccess || result.Error is ValidationFailed);
                    }

                    break;

                case 9 when cancelled.Count > 0:
                    Assert.IsType<InvalidState>((await SalesTestSupport.CancelAsync(_db, cancelled[random.Next(cancelled.Count)])).Error);
                    break;
            }
        }

        Assert.True(cancelled.Count > 5, $"Solo {cancelled.Count} cancelaciones");
        await AssertInvariantsAsync();
    }

    private async Task AssertInvariantsAsync()
    {
        await using var context = _db.CreateDbContext();
        var stocks = await context.ProductStocks.AsNoTracking().ToListAsync(Ct);
        var sawNegative = false;

        foreach (var stock in stocks)
        {
            var product = _products.Single(p => p.Id == stock.ProductId);
            var unit = UnitOfMeasure.Find(product.UnitCode)!;
            var movements = await context.InventoryMovements.AsNoTracking()
                .Where(m => m.ProductId == stock.ProductId)
                .OrderBy(m => m.Sequence)
                .ToListAsync(Ct);

            // Invariante 2: la existencia es la suma con signo de sus movimientos.
            var sum = movements.Sum(m => m.Type.IsIncrease() ? m.QuantityThousandths : -m.QuantityThousandths);
            Assert.Equal(sum, stock.OnHandThousandths);
            Assert.Equal(movements.Count, stock.MovementCount);
            Assert.Equal(Enumerable.Range(1, movements.Count), movements.Select(m => m.Sequence));

            long running = 0;
            foreach (var m in movements)
            {
                running += m.Type.IsIncrease() ? m.QuantityThousandths : -m.QuantityThousandths;
                Assert.Equal(running, m.ResultingStockThousandths);
                Assert.True(m.Quantity.FitsDecimals(unit.DecimalPlaces));
                Assert.True(m.ResultingStock.FitsDecimals(unit.DecimalPlaces));
                sawNegative |= m.ResultingStockThousandths < 0;

                // Un ajuste manual nunca deja la existencia bajo cero; solo las ventas la llevan ahí.
                if (m.Type == MovementType.AdjustOut)
                {
                    Assert.True(m.ResultingStockThousandths >= 0);
                }
            }
        }

        Assert.True(sawNegative, "La secuencia no produjo existencias negativas");

        // Invariante 1: Σ líneas = total = Σ pagos, y ventas y folios consistentes.
        var sales = await context.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .OrderBy(s => s.FolioNumber)
            .ToListAsync(Ct);
        Assert.Equal(Enumerable.Range(1, sales.Count).Select(n => (long)n), sales.Select(s => s.FolioNumber));

        var movementsById = await context.InventoryMovements.AsNoTracking().ToDictionaryAsync(m => m.Id, Ct);
        foreach (var sale in sales)
        {
            Assert.Equal(sale.TotalCents, sale.Lines.Sum(l => l.AmountCents));
            Assert.Equal(sale.TotalCents, sale.Payments.Sum(p => p.AmountCents));
            Assert.All(sale.Lines, l => Assert.Equal(
                SaleMath.LineAmount(l.Quantity, l.UnitPrice).Cents, l.AmountCents));

            foreach (var line in sale.Lines)
            {
                var tracks = _products.Single(p => p.Id == line.ProductId).TracksInventory;
                Assert.Equal(tracks, line.SaleMovementId is not null);
                if (line.SaleMovementId is { } saleMovement)
                {
                    var movement = movementsById[saleMovement];
                    Assert.Equal(MovementType.Sale, movement.Type);
                    Assert.Equal(line.QuantityThousandths, movement.QuantityThousandths);
                    Assert.Equal(sale.Folio, movement.Reference);
                }

                if (sale.Status == SaleStatus.Cancelled && line.SaleMovementId is not null)
                {
                    var back = movementsById[line.CancellationMovementId!.Value];
                    Assert.Equal(MovementType.SaleCancellation, back.Type);
                    Assert.Equal(line.QuantityThousandths, back.QuantityThousandths);
                }
                else
                {
                    Assert.Null(line.CancellationMovementId);
                }
            }
        }

        // Cada cancelación deja una entrada de bitácora.
        Assert.Equal(
            sales.Count(s => s.Status == SaleStatus.Cancelled),
            await context.AuditEntries.CountAsync(a => a.Action == "SALE_CANCELLED", Ct));
    }
}
