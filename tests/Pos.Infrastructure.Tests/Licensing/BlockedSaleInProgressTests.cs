using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Application.Sales.DiscardSaleDraft;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Domain.Discounts;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.Discounts;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 025, H6 escenarios 7 y 8 (FR-030a, Principio I): el vencimiento nunca interrumpe la venta en curso. En bloqueo
/// solo se sigue, cobra o descarta el borrador guardado; sus descuentos ya capturados se respetan aunque el módulo
/// venza, pero uno nuevo sigue la regla de módulo. Cada caso es una garantía distinta (plan, Complexity Tracking).
/// </summary>
public sealed class BlockedSaleInProgressTests : DiscountTestBase
{
    private static readonly LineDiscountInput Captured = new(DiscountMode.Percent, 500);

    private Product _product = null!;
    private Guid _draftId;

    private async Task StartSaleAsync()
    {
        Users.License = TestLicenses.Licensed(Db.Clock, LicensedModule.Discounts);
        _product = await ProductAsync("BLQ-1", 1000);
        _draftId = Guid.CreateVersion7();
        Assert.True((await SaveDraftAsync(_draftId, Captured)).IsSuccess);
    }

    private async Task<Result> SaveDraftAsync(Guid draftId, LineDiscountInput? discount)
    {
        await using var context = Db.CreateDbContext();
        var line = new DraftLineDto(
            _product.Id,
            1000,
            _product.Price.Cents,
            discount is null ? null : new DraftDiscountDto(discount.Mode, discount.Value, discount.ApprovalId));
        return await new SaveSaleDraftHandler(
                Users.Access(context),
                new SqliteSaleDraftStore(context, Db.Clock, Db.User),
                NullLogger<SaveSaleDraftHandler>.Instance,
                Users.License)
            .HandleAsync(new SaveSaleDraftCommand(draftId, [line]), Ct);
    }

    private Task<Result<ConfirmedSale>> ChargeAsync(Guid draftId, LineDiscountInput discount, long totalCents) =>
        Discounts.SellAsync(new ConfirmSaleCommand(draftId, [Line(_product, 1000, discount)], DiscountTestSupport.Cash(totalCents)));

    private async Task<int> SalesCountAsync()
    {
        await using var context = Db.CreateDbContext();
        return await context.Sales.CountAsync(Ct);
    }

    [Fact]
    public async Task EnBloqueo_LaVentaEnCursoSeCobraConSuDescuentoCapturado_YNoSeIniciaOtra()
    {
        await StartSaleAsync();
        Users.License = TestLicenses.Exactly(Db.Clock);
        Assert.True(Users.License.Current.IsBlocked);

        Assert.IsType<SystemNotActivated>((await SaveDraftAsync(Guid.CreateVersion7(), null)).Error);
        Assert.IsType<SystemNotActivated>((await ChargeAsync(Guid.CreateVersion7(), Captured, 950)).Error);
        Assert.Equal(0, await SalesCountAsync());

        Assert.True((await SaveDraftAsync(_draftId, Captured)).IsSuccess);
        var sale = await ChargeAsync(_draftId, Captured, 950);

        Assert.True(sale.IsSuccess, sale.Error?.ToString());
        Assert.Equal(950, sale.Value.TotalCents);
        Assert.IsType<SystemNotActivated>((await ChargeAsync(Guid.CreateVersion7(), Captured, 950)).Error);
    }

    [Fact]
    public async Task EnBloqueo_UnDescuentoNuevoODistinto_SigueLaReglaDeModulo()
    {
        await StartSaleAsync();
        Users.License = TestLicenses.Exactly(Db.Clock);

        var result = await ChargeAsync(_draftId, new LineDiscountInput(DiscountMode.Percent, 800), 920);

        Assert.Equal(LicensedModule.Discounts, Assert.IsType<ModuleNotLicensed>(result.Error).Module);
        Assert.Equal(0, await SalesCountAsync());
    }

    [Fact]
    public async Task SinBloqueo_ConDescuentosVencidos_LaVentaEnCursoSeCobraConSuDescuento()
    {
        await StartSaleAsync();
        Users.License = TestLicenses.Licensed(Db.Clock);
        Assert.False(Users.License.Current.IsBlocked);

        var sale = await ChargeAsync(_draftId, Captured, 950);

        Assert.True(sale.IsSuccess, sale.Error?.ToString());
        Assert.Equal(950, sale.Value.TotalCents);
    }

    [Fact]
    public async Task EnBloqueo_LaVentaEnCursoSePuedeDescartar_YRetomarTrasReiniciar()
    {
        await StartSaleAsync();
        Users.License = TestLicenses.Exactly(Db.Clock);

        // Un "reinicio" es un contexto nuevo: el borrador durable sigue ahí.
        await using (var context = Db.CreateDbContext())
        {
            var stored = await new SqliteSaleDraftStore(context, Db.Clock, Db.User).LoadAsync(Ct);
            Assert.Equal(_draftId, stored!.DraftId);
        }

        await using (var context = Db.CreateDbContext())
        {
            var discarded = await new DiscardSaleDraftHandler(Users.Access(context), new SqliteSaleDraftStore(context, Db.Clock, Db.User))
                .HandleAsync(Ct);
            Assert.True(discarded.IsSuccess);
        }

        Assert.IsType<SystemNotActivated>((await ChargeAsync(_draftId, Captured, 950)).Error);
    }
}
