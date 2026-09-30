using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>
/// Prueba de consistencia de inventario (obligatoria, constitución Principio VI): tras una secuencia
/// reproducible de movimientos válidos e inválidos, se cumplen los 6 invariantes de data-model.md.
/// </summary>
public sealed class InventoryConsistencyTests : IAsyncLifetime
{
    private static readonly string[] Units = ["H87", "KGM", "LTR", "GRM", "XBX"];
    private static readonly string[] QuantityTexts =
        ["1", "2", "3", "5", "10", "1.5", "0.250", "2.125", "0", "abc", "1.0005", "7,500", "100", "0.001"];

    private TestDb _db = null!;
    private List<Product> _products = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        foreach (var (unit, index) in Units.Select((u, i) => (u, i)))
        {
            _products.Add(await InventoryTestSupport.SeedProductAsync(_db, $"INV-{index}", unit));
        }
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Random_sequence_keeps_stock_equal_to_the_sum_of_movements()
    {
        var random = new Random(20260929);
        var types = Enum.GetValues<MovementType>();
        var accepted = 0;
        var rejected = 0;

        for (var i = 0; i < 400; i++)
        {
            var command = new RegisterMovementCommand(
                _products[random.Next(_products.Count)].Id,
                types[random.Next(types.Length)],
                QuantityTexts[random.Next(QuantityTexts.Length)],
                random.Next(3) == 0 ? null : "motivo",
                null);

            await using var context = _db.CreateDbContext();
            var result = await InventoryTestSupport.Handler(context).HandleAsync(command, Ct);
            if (result.IsSuccess)
            {
                accepted++;
            }
            else
            {
                rejected++;
                Assert.IsType<ValidationFailed>(result.Error);
            }
        }

        Assert.True(accepted > 50, $"Solo {accepted} movimientos válidos");
        Assert.True(rejected > 50, $"Solo {rejected} movimientos rechazados");
        await AssertInvariantsAsync();
    }

    private async Task AssertInvariantsAsync()
    {
        await using var context = _db.CreateDbContext();
        var stocks = await context.ProductStocks.AsNoTracking().ToListAsync(Ct);
        var units = Units.ToDictionary(u => u, u => UnitOfMeasure.Find(u)!);
        Assert.NotEmpty(stocks);

        foreach (var stock in stocks)
        {
            var product = _products.Single(p => p.Id == stock.ProductId);
            var unit = units[product.UnitCode];
            var movements = await context.InventoryMovements.AsNoTracking()
                .Where(m => m.ProductId == stock.ProductId)
                .OrderBy(m => m.Sequence)
                .ToListAsync(Ct);

            // 1. OnHand = suma algebraica de los movimientos.
            var sum = movements.Sum(m => m.Type.IsIncrease() ? m.QuantityThousandths : -m.QuantityThousandths);
            Assert.Equal(sum, stock.OnHandThousandths);

            // 2. MovementCount = COUNT y secuencias exactamente 1..N.
            Assert.Equal(movements.Count, stock.MovementCount);
            Assert.Equal(Enumerable.Range(1, movements.Count), movements.Select(m => m.Sequence));

            // 3. Cada existencia resultante es la anterior ± cantidad; la última es OnHand.
            long previous = 0;
            foreach (var m in movements)
            {
                previous += m.Type.IsIncrease() ? m.QuantityThousandths : -m.QuantityThousandths;
                Assert.Equal(previous, m.ResultingStockThousandths);

                // 4. Ninguna existencia resultante es negativa.
                Assert.True(m.ResultingStockThousandths >= 0);

                // 6. Las cantidades respetan los decimales de la unidad (SC-007).
                Assert.True(m.Quantity.FitsDecimals(unit.DecimalPlaces));
                Assert.True(m.ResultingStock.FitsDecimals(unit.DecimalPlaces));
            }

            Assert.Equal(stock.OnHandThousandths, movements[^1].ResultingStockThousandths);

            // 5. Solo el movimiento con Sequence = 1 puede ser INITIAL.
            Assert.DoesNotContain(movements.Skip(1), m => m.Type == MovementType.Initial);
        }
    }
}
