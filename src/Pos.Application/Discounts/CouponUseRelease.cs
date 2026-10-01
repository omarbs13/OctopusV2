using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing;
using Pos.Domain.Discounts;
using Pos.Domain.Licensing;
using Pos.Domain.Sales;

namespace Pos.Application.Discounts;

/// <summary>
/// Devuelve al cupón el uso de una venta cancelada por completo (015, FR-014) con <c>COUPON_USE_RELEASED</c>,
/// dentro de la transacción de la cancelación. Una devolución parcial no lo usa. Sin licencia del módulo
/// Descuentos el uso no se devuelve (spec, casos límite).
/// </summary>
public sealed partial class CouponUseRelease
{
    private readonly ICouponRepository _coupons;
    private readonly IAuditLog _audit;
    private readonly ILogger<CouponUseRelease> _logger;
    private readonly ILicenseState? _license;

    public CouponUseRelease(ICouponRepository coupons, IAuditLog audit, ILogger<CouponUseRelease> logger, ILicenseState? license = null)
    {
        _coupons = coupons;
        _audit = audit;
        _logger = logger;
        _license = license;
    }

    public async Task ReleaseForCancelledSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (_license?.IsModuleActive(LicensedModule.Discounts) == false)
        {
            return;
        }

        foreach (var discount in sale.Discounts.Where(d => d.Kind == DiscountKind.Coupon && d.CouponId is not null))
        {
            var coupon = await _coupons.GetAsync(discount.CouponId!.Value, cancellationToken);
            if (coupon is null)
            {
                continue;
            }

            coupon.ReleaseUse();
            _audit.Add(
                AuditActions.CouponUseReleased,
                AuditActions.CouponEntity,
                coupon.Id,
                $"Cupón {coupon.Code}. Venta {sale.Folio} cancelada. Usos realizados: {coupon.UsesCount}");
            LogReleased(coupon.Id, coupon.Code, sale.Id, sale.Folio);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Uso de cupón devuelto por cancelación. CouponId={CouponId} Codigo={Code} SaleId={SaleId} Folio={Folio}")]
    private partial void LogReleased(Guid couponId, string code, Guid saleId, string folio);
}
