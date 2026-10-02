using System.Diagnostics;
using Pos.Application.Reports.GetPurchaseReport;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>
/// SC-005: con 10,000 compras, cada consulta del reporte tarda menos de 2 s (objetivo &lt; 300 ms). Explícita: se
/// ejecuta a mano en Release (quickstart.md §7), no en cada corrida.
/// </summary>
public sealed class PurchaseReportPerformanceTests : IAsyncLifetime
{
    private const int PurchaseCount = 10_000;

    private TestDb _db = null!;
    private PurchaseTestSupport _purchases = null!;
    private Guid _norte;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _purchases = await PurchaseTestSupport.CreateAsync(_db);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact(Explicit = true)]
    public async Task PrimeraPagina_ConCadaFiltroYConTodos_TardaMenosDeDosSegundos()
    {
        await SeedAsync();
        var today = _purchases.Today;
        GetPurchaseReportQuery[] queries =
        [
            new(null, null, null, null, null, false),
            new(_norte, null, null, null, null, false),
            new(null, today.AddDays(-60), today.AddDays(-30), null, null, false),
            new(null, null, null, "50.00", "500.00", false),
            new(null, null, null, null, null, true),
            new(_norte, today.AddDays(-60), today.AddDays(-30), "50.00", "500.00", true),
        ];

        foreach (var query in queries)
        {
            var watch = Stopwatch.StartNew();
            var result = await _purchases.ReportAsync(query);
            watch.Stop();

            Assert.True(result.IsSuccess, result.Error?.ToString());
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{query}: {watch.Elapsed.TotalMilliseconds:N0} ms");
            TestContext.Current.SendDiagnosticMessage($"{query}: {watch.Elapsed.TotalMilliseconds:N0} ms");
        }
    }

    /// <summary>Siembra con el dominio real en lotes (compra, movimiento y enlace), sin pasar por el caso de uso.</summary>
    private async Task SeedAsync()
    {
        _norte = await _purchases.Suppliers.CreateOkAsync("Norte");
        var sur = await _purchases.Suppliers.CreateOkAsync("Sur");
        var product = await _purchases.ProductAsync("PERF");
        var today = _purchases.Today;
        var piece = UnitOfMeasure.Piece;

        for (var batch = 0; batch < PurchaseCount / 500; batch++)
        {
            await using var context = _db.CreateDbContext();
            var stock = context.ProductStocks.SingleOrDefault(s => s.ProductId == product.Id);
            if (stock is null)
            {
                stock = ProductStock.Start(product.Id);
                context.ProductStocks.Add(stock);
            }

            for (var i = 0; i < 500; i++)
            {
                var n = (batch * 500) + i;
                var purchase = Purchase.Register(
                    n % 2 == 0 ? _norte : sur,
                    n % 2 == 0 ? "Norte" : "Sur",
                    $"F-{n:00000}",
                    today.AddDays(-(n % 365)),
                    today,
                    [new PurchaseLineDraft(product.Id, product.Name, product.Sku, product.UnitCode, 1_000 + (n % 50 * 1_000), 1_000)],
                    n % 500);
                var movement = stock.RecordPurchase(Quantity.FromThousandths(purchase.Lines[0].QuantityThousandths), piece, true, true, purchase.InvoiceNumber);
                context.InventoryMovements.Add(movement);
                purchase.LinkMovement(product.Id, movement.Id);
                context.Purchases.Add(purchase);
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
