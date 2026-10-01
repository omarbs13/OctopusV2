using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Sales;

/// <summary>
/// Descuento aplicado en una venta registrada (015, FR-015): de línea, global o de cupón. Es parte
/// inmutable del agregado <see cref="Sale"/>, igual que <see cref="SaleLine"/>.
/// </summary>
public sealed class SaleDiscount
{
    private SaleDiscount()
    {
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    /// <summary>Línea con el descuento; obligatoria si <see cref="Kind"/> es <see cref="DiscountKind.Line"/>.</summary>
    public Guid? SaleLineId { get; private set; }

    public DiscountKind Kind { get; private set; }

    public DiscountMode Mode { get; private set; }

    /// <summary>Valor capturado: puntos base o centavos.</summary>
    public long Value { get; private set; }

    /// <summary>Monto descontado, mayor que 0.</summary>
    public long AmountCents { get; private set; }

    public Guid? CouponId { get; private set; }

    /// <summary>Copia del código del cupón al vender.</summary>
    public string? CouponCode { get; private set; }

    /// <summary>Usuario que aplicó el descuento (el que vende).</summary>
    public Guid AppliedBy { get; private set; }

    /// <summary>Administrador que autorizó, si el descuento superó el límite.</summary>
    public Guid? AuthorizedBy { get; private set; }

    /// <summary>Momento del cobro, en UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    public DiscountValue Discount => DiscountValue.Create(Mode, Value);

    public static SaleDiscount ForLine(Guid saleLineId, DiscountValue value, long amountCents, Guid appliedBy, Guid? authorizedBy, DateTime utcNow) =>
        Create(DiscountKind.Line, saleLineId, value, amountCents, null, null, appliedBy, authorizedBy, utcNow);

    public static SaleDiscount ForOrder(DiscountValue value, long amountCents, Guid appliedBy, Guid? authorizedBy, DateTime utcNow) =>
        Create(DiscountKind.Order, null, value, amountCents, null, null, appliedBy, authorizedBy, utcNow);

    public static SaleDiscount ForCoupon(Guid couponId, string couponCode, DiscountValue value, long amountCents, Guid appliedBy, DateTime utcNow) =>
        Create(DiscountKind.Coupon, null, value, amountCents, couponId, couponCode, appliedBy, null, utcNow);

    private static SaleDiscount Create(
        DiscountKind kind,
        Guid? saleLineId,
        DiscountValue value,
        long amountCents,
        Guid? couponId,
        string? couponCode,
        Guid appliedBy,
        Guid? authorizedBy,
        DateTime utcNow)
    {
        if (amountCents is <= 0 or > Money.MaxCents)
        {
            throw new DomainException("El monto del descuento debe ser mayor que 0.");
        }

        if (appliedBy == Guid.Empty)
        {
            throw new DomainException("El descuento debe indicar quién lo aplicó.");
        }

        if (couponCode is { Length: > Coupon.CodeMaxLength })
        {
            throw new DomainException("El código del cupón no es válido.");
        }

        var checkedValue = DiscountValue.Create(value.Mode, value.Raw);
        return new SaleDiscount
        {
            Id = Guid.CreateVersion7(),
            SaleLineId = saleLineId,
            Kind = kind,
            Mode = checkedValue.Mode,
            Value = checkedValue.Raw,
            AmountCents = amountCents,
            CouponId = couponId,
            CouponCode = couponCode,
            AppliedBy = appliedBy,
            AuthorizedBy = authorizedBy,
            CreatedAt = utcNow,
        };
    }
}
