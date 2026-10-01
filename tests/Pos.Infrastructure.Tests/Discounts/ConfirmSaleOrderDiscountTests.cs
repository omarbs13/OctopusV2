using Pos.Application.Abstractions;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>015, Historia 2, FR-004 y SC-001: el descuento de venta se reparte y las líneas suman el total.</summary>
public sealed class ConfirmSaleOrderDiscountTests : DiscountTestBase
{
    [Fact]
    public async Task DescuentoGlobalIgualAlLimite_CobraSinAprobacionYLoRepartePorResto()
    {
        // Quickstart, escenario 6: subtotal $150.00 con $15.00 (exactamente 10 %).
        var c = await ProductAsync("ORD-C", 10_000);
        var a = await ProductAsync("ORD-A", 5_000);

        var result = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [Line(c, 1000), Line(a, 1000)],
            DiscountTestSupport.Cash(13_500),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Amount, 1_500)));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var sale = await LoadSaleAsync(result.Value.SaleId);
        Assert.Equal(13_500, sale.TotalCents);
        Assert.Equal(sale.TotalCents, sale.Lines.Sum(l => l.AmountCents));
        Assert.Equal(1_500, sale.Lines.Sum(l => l.OrderDiscountCents));
        Assert.Equal([1_000, 500], sale.Lines.OrderBy(l => l.Position).Select(l => l.OrderDiscountCents));
        var discount = Assert.Single(sale.Discounts);
        Assert.Equal(DiscountKind.Order, discount.Kind);
        Assert.Null(discount.AuthorizedBy);
    }

    [Fact]
    public async Task DescuentoDeLineaYGlobal_ElGlobalSeCalculaSobreElSubtotalYaDescontado()
    {
        var a = await ProductAsync("ORD-A2", 5_000);
        var c = await ProductAsync("ORD-C2", 10_000);

        // A: 2 × 50 = 100 − 10 % = 90; C: 100; subtotal 190; 10 % = 19; total 171.
        var result = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [Line(a, 2000, Percent(1000)), Line(c, 1000)],
            DiscountTestSupport.Cash(17_100),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Percent, 1000)));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var sale = await LoadSaleAsync(result.Value.SaleId);
        Assert.Equal(17_100, sale.TotalCents);
        Assert.Equal(19_000, sale.SubtotalCents);
        Assert.Equal(2_900, sale.DiscountCents);
        Assert.Equal(sale.DiscountCents, sale.Discounts.Sum(d => d.AmountCents));
        Assert.Equal(sale.TotalCents, sale.Lines.Sum(l => l.AmountCents));
    }

    [Fact]
    public async Task DescuentoGlobalSobreElLimite_ExigeAprobacionDeVenta()
    {
        var c = await ProductAsync("ORD-C3", 10_000);
        var draftId = Guid.CreateVersion7();
        ConfirmSaleCommand Command(Guid? approvalId) => new(
            draftId,
            [Line(c, 2000)],
            DiscountTestSupport.Cash(15_000),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Percent, 2500, approvalId));

        var without = await Discounts.SellAsync(Command(null));
        Assert.Equal(new DiscountApprovalRequired(DiscountScope.Order, null), without.Error);

        var approval = await Discounts.ApproveAsync(new ApproveDiscountCommand(
            draftId, DiscountScope.Order, null, DiscountMode.Percent, 2500, 20_000, Discounts.Grant()));
        Assert.True(approval.IsSuccess, approval.Error?.ToString());
        var result = await Discounts.SellAsync(Command(approval.Value.ApprovalId));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(Users.Admin.Id, Assert.Single((await LoadSaleAsync(result.Value.SaleId)).Discounts).AuthorizedBy);
    }

    [Fact]
    public async Task DescuentoGlobalDeMontoMayorQueElSubtotal_SeRetira()
    {
        var a = await ProductAsync("ORD-A4", 5_000);

        var result = await Discounts.SellAsync(new ConfirmSaleCommand(
            Guid.CreateVersion7(),
            [Line(a, 1000)],
            DiscountTestSupport.Cash(5_000),
            OrderDiscount: OrderDiscountInput.Manual(DiscountMode.Amount, 6_000)));

        Assert.Equal(new OrderDiscountRemoved(6_000), result.Error);
    }
}
