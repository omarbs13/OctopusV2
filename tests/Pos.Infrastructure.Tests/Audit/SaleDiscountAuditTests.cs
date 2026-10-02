using Microsoft.EntityFrameworkCore;
using Pos.Application.Audit;
using Pos.Infrastructure.Tests.Discounts;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>018 (FR-008, SC-007) sobre SQLite real: una sola entrada por venta con descuento, autorizado o no.</summary>
public sealed class SaleDiscountAuditTests : DiscountTestBase
{
    [Fact]
    public async Task DescuentoDeLineaSinAutorizacion_UnaEntradaConUnCambioPorDescuento()
    {
        var product = await ProductAsync("DSC-AUD", 5_000);

        // 2 × $50.00 = $100.00 con 10 %, dentro del límite: sin autorización.
        var sold = await Discounts.SellAsync(new(Guid.CreateVersion7(), [Line(product, 2000, Percent(1000))], DiscountTestSupport.Cash(9_000)));

        Assert.True(sold.IsSuccess, sold.Error?.ToString());
        await using var context = Db.CreateDbContext();
        var entry = Assert.Single(await context.AuditEntries.AsNoTracking().Where(e => e.Action == AuditActions.SaleDiscountsApplied).ToListAsync(Ct));
        Assert.Equal(sold.Value.SaleId, entry.EntityId);
        Assert.Equal($"Venta {sold.Value.Folio}", entry.EntityName);
        Assert.Null(entry.AuthorizedBy);
        var change = Assert.Single(entry.Changes);
        Assert.Equal($"Producto {product.Name}", change.Field);
        Assert.Equal("$100.00", change.Before);
        Assert.StartsWith("$90.00 · Descuento de línea 10%", change.After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VentaSinDescuento_NoCreaEntrada()
    {
        var product = await ProductAsync("DSC-SIN", 5_000);

        var sold = await Discounts.SellAsync(new(Guid.CreateVersion7(), [Line(product, 1000)], DiscountTestSupport.Cash(5_000)));

        Assert.True(sold.IsSuccess, sold.Error?.ToString());
        Assert.Equal(0, await AuditCountAsync(AuditActions.SaleDiscountsApplied));
    }
}
