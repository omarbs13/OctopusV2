using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Tests.Discounts;

/// <summary>015, research §9: estado por fecha y usos, y edición bloqueada con usos (FR-010, SC-004).</summary>
public sealed class CouponTests
{
    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static Coupon Verano(int? limit = 100) =>
        Coupon.Create(" verano10 ", DiscountValue.Percent(1000), October1, October31, limit);

    [Fact]
    public void Create_NormalizaElCodigoYRechazaFormatoInvalido()
    {
        Assert.Equal("VERANO10", Verano().Code);
        Assert.Throws<DomainException>(() => Coupon.Create("AB", DiscountValue.Percent(1000), October1, October31, null));
        Assert.Throws<DomainException>(() => Coupon.Create("CON ESPACIO", DiscountValue.Percent(1000), October1, October31, null));
        Assert.Throws<DomainException>(() => Coupon.Create("FECHAS", DiscountValue.Percent(1000), October31, October1, null));
    }

    [Fact]
    public void StatusOn_LosDiasDeInicioYFinSonValidosCompletos_YRespetaLaPrecedencia()
    {
        var coupon = Verano(limit: 1);

        Assert.Equal(CouponStatus.NotStarted, coupon.StatusOn(new DateOnly(2026, 9, 30)));
        Assert.Equal(CouponStatus.Active, coupon.StatusOn(October1));
        Assert.Equal(CouponStatus.Active, coupon.StatusOn(October31));
        Assert.Equal(CouponStatus.Expired, coupon.StatusOn(new DateOnly(2026, 11, 1)));

        coupon.ConsumeUse(October1);
        Assert.Equal(CouponStatus.Exhausted, coupon.StatusOn(new DateOnly(2026, 11, 1)));
        Assert.Throws<DomainException>(() => coupon.ConsumeUse(October1));

        coupon.Deactivate();
        Assert.Equal(CouponStatus.Inactive, coupon.StatusOn(October1));
    }

    [Fact]
    public void ConUsos_NoSeCambiaCodigoNiValor_NiElLimiteDebajoDeLosUsos()
    {
        var coupon = Verano(limit: 5);
        coupon.ConsumeUse(October1);
        coupon.ConsumeUse(October1);

        Assert.Throws<DomainException>(() => coupon.Edit("OTRO10", DiscountValue.Percent(1000)));
        Assert.Throws<DomainException>(() => coupon.Edit("VERANO10", DiscountValue.Percent(1500)));
        Assert.Throws<DomainException>(() => coupon.Update(October1, October31, 1));

        coupon.Update(October1, new DateOnly(2026, 11, 30), 2);
        Assert.Equal(0, coupon.RemainingUses);
    }

    [Fact]
    public void ReleaseUse_DevuelveUnUsoYNoBajaDeCero()
    {
        var coupon = Verano(limit: 1);
        coupon.ConsumeUse(October1);

        coupon.ReleaseUse();
        coupon.ReleaseUse();

        Assert.Equal(0, coupon.UsesCount);
        Assert.Equal(CouponStatus.Active, coupon.StatusOn(October1));
    }
}
