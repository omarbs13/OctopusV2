using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Returns;
using Pos.Infrastructure.Tests.Discounts;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Returns;

/// <summary>015, FR-020: la devolución parcial (013) devuelve lo efectivamente pagado, neto de descuentos.</summary>
public sealed class ReturnsWithDiscountsTests : DiscountTestBase
{
    [Fact]
    public async Task DevolucionParcial_DeUnaLineaConDescuentoDeLineaYGlobal_DevuelveLoPagado()
    {
        var a = await ProductAsync("RET-A", 5_000);
        var c = await ProductAsync("RET-C", 10_000);

        // A: 2 × 50 = 100 − 10 % = 90; C: 100; subtotal 190 − 10 % (19) = 171.
        // Reparto de 19.00 por 90:100 → A 9.00, C 10.00; A pagó 81.00 por 2 unidades.
        var sale = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [Line(a, 2000, Percent(1000)), Line(c, 1000)],
            DiscountTestSupport.Cash(17_100),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Percent, 1000)));
        Assert.True(sale.IsSuccess, sale.Error?.ToString());
        var lineA = await Discounts.Returns.LineIdAsync(sale.Value.SaleId, 1);

        var result = await Discounts.Returns.ReturnAsync(sale.Value.SaleId, [new ReturnLineRequest(lineA, 1000)]);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(4_050, result.Value.TotalCents);
    }
}
