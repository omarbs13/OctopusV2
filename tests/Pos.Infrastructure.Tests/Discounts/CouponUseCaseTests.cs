using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Discounts;
using Pos.Application.Discounts.Coupons.SaveCoupon;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Products;
using Pos.Domain.Returns;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>015, Historia 3, FR-012, FR-014 y SC-004: ningún cupón supera sus usos y los inválidos se rechazan.</summary>
public sealed class CouponUseCaseTests : DiscountTestBase
{
    private async Task<Guid> CreateCouponAsync(string code, int? limit, DateOnly? startsOn = null, DateOnly? endsOn = null)
    {
        Users.As(Users.Admin);
        var result = await Discounts.SaveCouponAsync(new SaveCouponCommand(
            null, code, DiscountMode.Percent, "10", startsOn ?? Discounts.Today, endsOn ?? Discounts.Today.AddDays(30), limit));
        Users.As(Users.Cashier);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>$100.00 con el cupón: total $90.00.</summary>
    private static ConfirmSaleCommand SaleWithCoupon(Product product, string code, long thousandths = 1000) =>
        new(
            Guid.CreateVersion7(),
            [Line(product, thousandths)],
            DiscountTestSupport.Cash(product.Price.Cents * thousandths / 1000 * 9 / 10),
            OrderDiscount: OrderDiscountInput.Coupon(code));

    [Fact]
    public async Task UltimoUso_DosCobrosCasiAlMismoTiempo_SoloUnoUsaElCupon()
    {
        var product = await ProductAsync("CUP-1", 10_000);
        await CreateCouponAsync("PRUEBA10", limit: 1);
        await SalesTestSupport.EnsureShiftAsync(Db);

        var results = await Task.WhenAll(
            Discounts.SellAsync(SaleWithCoupon(product, "prueba10")),
            Discounts.SellAsync(SaleWithCoupon(product, " PRUEBA10 ")));

        Assert.Single(results, r => r.IsSuccess);
        var rejected = Assert.IsType<CouponNotValid>(Assert.Single(results, r => !r.IsSuccess).Error);
        Assert.Equal(CouponStatus.Exhausted, rejected.Status);
        Assert.Equal(1, (await Discounts.CouponAsync("PRUEBA10")).UsesCount);
    }

    [Fact]
    public async Task CuponVencidoAlCobrar_NoRegistraLaVenta()
    {
        var product = await ProductAsync("CUP-2", 10_000);
        await CreateCouponAsync("VENCIDO", limit: null, Discounts.Today.AddDays(-10), Discounts.Today.AddDays(-1));

        var result = await Discounts.SellAsync(SaleWithCoupon(product, "VENCIDO"));

        var rejected = Assert.IsType<CouponNotValid>(result.Error);
        Assert.Equal(CouponStatus.Expired, rejected.Status);
        await using var context = Db.CreateDbContext();
        Assert.Empty(await context.Sales.ToListAsync(Ct));
    }

    [Fact]
    public async Task CancelacionCompleta_DevuelveElUso_YLaDevolucionParcialNo()
    {
        var product = await ProductAsync("CUP-3", 10_000);
        await CreateCouponAsync("VERANO10", limit: 5);

        var cancelled = await Discounts.SellAsync(SaleWithCoupon(product, "VERANO10"));
        var partial = await Discounts.SellAsync(SaleWithCoupon(product, "VERANO10", thousandths: 2000));
        Assert.True(cancelled.IsSuccess, cancelled.Error?.ToString());
        Assert.True(partial.IsSuccess, partial.Error?.ToString());
        Assert.Equal(2, (await Discounts.CouponAsync("VERANO10")).UsesCount);

        var cancel = await Discounts.CancelAsync(cancelled.Value.SaleId);
        Assert.True(cancel.IsSuccess, cancel.Error?.ToString());
        Assert.Equal(1, (await Discounts.CouponAsync("VERANO10")).UsesCount);
        Assert.Equal(1, await AuditCountAsync(AuditActions.CouponUseReleased));

        var line = await Discounts.Returns.LineIdAsync(partial.Value.SaleId, 1);
        var returned = await Discounts.Returns.ReturnAsync(partial.Value.SaleId, [new ReturnLineRequest(line, 1000)]);
        Assert.True(returned.IsSuccess, returned.Error?.ToString());
        Assert.Equal(1, (await Discounts.CouponAsync("VERANO10")).UsesCount);
    }

    [Fact]
    public async Task CodigoQueCoincideConUnProducto_OConOtroCupon_SeRechaza()
    {
        await ProductAsync("SKU-CHOCA", 1_000);
        await CreateCouponAsync("UNICO", limit: null);
        Users.As(Users.Admin);

        var collides = await Discounts.SaveCouponAsync(new SaveCouponCommand(
            null, "sku-choca", DiscountMode.Percent, "10", Discounts.Today, Discounts.Today, null));
        var duplicated = await Discounts.SaveCouponAsync(new SaveCouponCommand(
            null, "unico", DiscountMode.Amount, "5", Discounts.Today, Discounts.Today, null));

        Assert.IsType<CodeCollidesWithProduct>(collides.Error);
        Assert.Equal(new Duplicate(DiscountFields.Code), duplicated.Error);
    }
}
