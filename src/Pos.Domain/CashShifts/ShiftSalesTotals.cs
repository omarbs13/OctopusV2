namespace Pos.Domain.CashShifts;

/// <summary>
/// Totales de las ventas de un turno, todo en centavos (<c>long</c>: nunca lanza aunque el turno
/// exceda el máximo de <c>Money</c>). <c>CashSalesCents</c> es el efectivo aplicado (ya neto de
/// cambio) de todas las ventas del turno y <c>CashCancelledCents</c> el de las canceladas; tarjeta,
/// transferencia y total vendido son de las completadas (FR-019a).
/// </summary>
public sealed record ShiftSalesTotals(
    int SalesCount,
    int CancelledCount,
    long TotalSoldCents,
    long CashSalesCents,
    long CashCancelledCents,
    long CardCents,
    long TransferCents)
{
    public static ShiftSalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}
