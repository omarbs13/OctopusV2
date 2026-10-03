using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Sales.ConfirmSale;

namespace Pos.Application.Sales;

/// <summary>
/// La venta en curso es el borrador durable del usuario (025, research §10.2, blocked-mode §1): en bloqueo solo se
/// puede seguir, cobrar o descartar ese borrador, y sus partes ya capturadas se respetan aunque su módulo venza.
/// </summary>
internal static class DraftInProgress
{
    /// <summary>
    /// Con el sistema bloqueado, solo el borrador guardado con líneas es la venta en curso; cualquier otro
    /// <paramref name="draftId"/> es una venta nueva y se rechaza con <see cref="SystemNotActivated"/>.
    /// </summary>
    public static SystemNotActivated? RejectNewSaleWhenBlocked(ILicenseState? license, StoredDraft? stored, Guid draftId) =>
        LicenseGate.WhenBlocked(license) is { } blocked
            && (stored is null || stored.Lines.Count == 0 || stored.DraftId != draftId)
            ? blocked
            : null;

    /// <summary>
    /// Los descuentos del comando son los ya capturados en el borrador guardado (blocked-mode §1 "Partes ya
    /// capturadas"): cada descuento de línea coincide con el de su producto en el borrador y el de la venta
    /// coincide con el del borrador. Un descuento nuevo o distinto no coincide y sigue la regla de módulo.
    /// </summary>
    public static bool HasOnlyCapturedDiscounts(StoredDraft? stored, ConfirmSaleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (stored is null || stored.DraftId != command.DraftId)
        {
            return false;
        }

        foreach (var line in command.Lines.Where(l => l.Discount is not null))
        {
            var discount = line.Discount!;
            var captured = new DraftDiscountDto(discount.Mode, discount.Value, discount.ApprovalId);
            if (!stored.Lines.Any(s => s.ProductId == line.ProductId && s.Discount == captured))
            {
                return false;
            }
        }

        return command.OrderDiscount switch
        {
            null => true,
            { IsCoupon: true } coupon => stored.OrderDiscount is { IsCoupon: true } kept
                && string.Equals(kept.Code, coupon.CouponCode, StringComparison.OrdinalIgnoreCase),
            var manual => stored.OrderDiscount is { IsCoupon: false } kept
                && kept.Mode == manual.Mode && kept.Value == manual.Value && kept.ApprovalId == manual.ApprovalId,
        };
    }
}
