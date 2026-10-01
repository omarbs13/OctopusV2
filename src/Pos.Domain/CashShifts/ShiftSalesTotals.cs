namespace Pos.Domain.CashShifts;

/// <summary>
/// Totales de las ventas de un turno, todo en centavos (<c>long</c>: nunca lanza aunque el turno
/// exceda el máximo de <c>Money</c>). <c>CashSalesCents</c> es el efectivo aplicado (ya neto de
/// cambio) de todas las ventas del turno y <c>CashCancelledCents</c> el de las canceladas; tarjeta,
/// transferencia y total vendido son de las completadas (FR-019a). Desde 013 el total vendido es neto
/// de devoluciones parciales; <c>CashRefundsCents</c> es el efectivo devuelto en este turno,
/// <c>NonCashRefundsCents</c> los reintegros de tarjeta y transferencia anotados y
/// <c>CreditNotesIssuedCents</c> lo emitido en notas de crédito.
/// </summary>
public sealed record ShiftSalesTotals(
    int SalesCount,
    int CancelledCount,
    long TotalSoldCents,
    long CashSalesCents,
    long CashCancelledCents,
    long CardCents,
    long TransferCents,
    long CashRefundsCents = 0,
    long NonCashRefundsCents = 0,
    long CreditNotesIssuedCents = 0)
{
    public static ShiftSalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}
