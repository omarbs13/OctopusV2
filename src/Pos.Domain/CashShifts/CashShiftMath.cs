namespace Pos.Domain.CashShifts;

/// <summary>Reglas puras del efectivo de un turno, todo en centavos (research §3 y §4).</summary>
public static class CashShiftMath
{
    /// <summary>
    /// Efectivo esperado (FR-015): fondo inicial + efectivo de ventas (neto de cambio) − efectivo
    /// de ventas canceladas heredado (sin devolución registrada) + ingresos − retiros − reintegros en efectivo
    /// + abonos de clientes en efectivo − anulaciones de abonos en efectivo (014, SC-007).
    /// </summary>
    public static long ExpectedCash(long openingFloatCents, ShiftSalesTotals totals, long depositsCents, long withdrawalsCents)
    {
        ArgumentNullException.ThrowIfNull(totals);
        return openingFloatCents + totals.CashSalesCents - totals.CashCancelledCents + depositsCents - withdrawalsCents - totals.CashRefundsCents
            + totals.CustomerPaymentsCashCents - totals.CustomerPaymentVoidsCashCents;
    }

    /// <summary>Indica si se puede devolver el efectivo de una venta sin dejar el esperado en negativo (FR-008).</summary>
    public static bool CanRefund(long expectedCents, long saleCashCents) => expectedCents - saleCashCents >= 0;
}
