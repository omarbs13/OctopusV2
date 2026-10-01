using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>015, research §7 y SC-002: al cobrar se revalida cada descuento contra el límite vigente y su aprobación.</summary>
public sealed class ConfirmSaleApprovalTests : DiscountTestBase
{
    private Product _a = null!;

    private async Task<Product> AAsync() => _a ??= await ProductAsync("DSC-A", 5_000);

    /// <summary>2 × $50.00 = $100.00 con el descuento indicado.</summary>
    private async Task<ConfirmSaleCommand> SaleAsync(Guid draftId, LineDiscountInput discount, long totalCents) =>
        new(draftId, [Line(await AAsync(), 2000, discount)], DiscountTestSupport.Cash(totalCents));

    private async Task<Guid> ApproveAsync(Guid draftId, long amountCents)
    {
        var result = await Discounts.ApproveAsync(new ApproveDiscountCommand(
            draftId, DiscountScope.Line, (await AAsync()).Id, DiscountMode.Amount, amountCents, 10_000, Discounts.Grant()));
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.ApprovalId;
    }

    [Fact]
    public async Task DentroDelLimite_CobraSinAprobacionYGuardaElDescuentoDeLinea()
    {
        var result = await Discounts.SellAsync(await SaleAsync(Guid.CreateVersion7(), Percent(1000), 9_000));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var sale = await LoadSaleAsync(result.Value.SaleId);
        Assert.Equal(9_000, sale.TotalCents);
        Assert.Equal(1_000, sale.DiscountCents);
        var line = Assert.Single(sale.Lines);
        Assert.Equal(10_000, line.OriginalAmountCents);
        Assert.Equal(1_000, line.LineDiscountCents);
        Assert.Equal(9_000, line.AmountCents);
        var discount = Assert.Single(sale.Discounts);
        Assert.Equal(DiscountKind.Line, discount.Kind);
        Assert.Equal(line.Id, discount.SaleLineId);
        Assert.Equal(Users.Cashier.Id, discount.AppliedBy);
        Assert.Null(discount.AuthorizedBy);
    }

    [Fact]
    public async Task SobreElLimite_SinAprobacionOConAprobacionDeOtraVentaOInsuficiente_NoCobra()
    {
        var draftId = Guid.CreateVersion7();
        var productId = (await AAsync()).Id;

        var none = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500), 8_500));
        Assert.Equal(new DiscountApprovalRequired(DiscountScope.Line, productId), none.Error);

        var otherDraft = await ApproveAsync(Guid.CreateVersion7(), 1_500);
        var wrongDraft = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500, otherDraft), 8_500));
        Assert.IsType<DiscountApprovalRequired>(wrongDraft.Error);

        // Aprobó 12 %; el descuento actual es 15 %.
        var smaller = await ApproveAsync(draftId, 1_200);
        var insufficient = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500, smaller), 8_500));
        Assert.IsType<DiscountApprovalRequired>(insufficient.Error);
        Assert.Equal(0, await AppliedAuthorizedCountAsync());
    }

    [Fact]
    public async Task SobreElLimite_ConAprobacionQueLoCubre_CobraConAutorizadorYBitacora()
    {
        var draftId = Guid.CreateVersion7();
        var approval = await ApproveAsync(draftId, 1_500);

        var result = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500, approval), 8_500));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var discount = Assert.Single((await LoadSaleAsync(result.Value.SaleId)).Discounts);
        Assert.Equal(Users.Cashier.Id, discount.AppliedBy);
        Assert.Equal(Users.Admin.Id, discount.AuthorizedBy);
        Assert.Equal(1, await AppliedAuthorizedCountAsync());
    }

    [Fact]
    public async Task Administrador_SinAprobacionGuardada_TambienNecesitaAprobarAntesDeCobrar()
    {
        Users.As(Users.Admin);
        var draftId = Guid.CreateVersion7();

        var without = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500), 8_500));
        Assert.IsType<DiscountApprovalRequired>(without.Error);

        // El Administrador aprueba sin concesión y queda como aplicador y autorizador.
        var approval = await Discounts.ApproveAsync(new ApproveDiscountCommand(
            draftId, DiscountScope.Line, (await AAsync()).Id, DiscountMode.Amount, 1_500, 10_000, null));
        Assert.True(approval.IsSuccess, approval.Error?.ToString());
        var result = await Discounts.SellAsync(await SaleAsync(draftId, Amount(1_500, approval.Value.ApprovalId), 8_500));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var discount = Assert.Single((await LoadSaleAsync(result.Value.SaleId)).Discounts);
        Assert.Equal(Users.Admin.Id, discount.AppliedBy);
        Assert.Equal(Users.Admin.Id, discount.AuthorizedBy);
    }

    [Fact]
    public async Task LimiteBajadoDespuesDeAplicar_SeRevalidaAlCobrar()
    {
        Discounts.Settings.Current = new DiscountSettings { LimitBasisPoints = 500 };

        var result = await Discounts.SellAsync(await SaleAsync(Guid.CreateVersion7(), Percent(1000), 9_000));

        Assert.IsType<DiscountApprovalRequired>(result.Error);
    }

    [Fact]
    public async Task DescuentoDel100PorCientoAutorizado_CobraConTotalCeroYSinPagos()
    {
        var draftId = Guid.CreateVersion7();
        var approval = await ApproveAsync(draftId, 10_000);

        var result = await Discounts.SellAsync(await SaleAsync(draftId, Amount(10_000, approval), 0));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var sale = await LoadSaleAsync(result.Value.SaleId);
        Assert.Equal(0, sale.TotalCents);
        Assert.Equal(10_000, sale.DiscountCents);
        Assert.Empty(sale.Payments);
    }
}
