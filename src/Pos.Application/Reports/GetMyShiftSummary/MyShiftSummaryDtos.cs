using Pos.Domain.CashShifts;

namespace Pos.Application.Reports.GetMyShiftSummary;

/// <summary>Ingreso o retiro de efectivo del turno propio.</summary>
public sealed record MyShiftMovement(DateTime CreatedAtUtc, CashMovementType Type, long AmountCents, string Reason);

/// <summary>
/// Resumen del turno propio del Cajero (Historia 6). Efectivo esperado, contado y diferencia solo vienen
/// con el turno cerrado: mientras está abierto el arqueo es ciego (008).
/// </summary>
public sealed record MyShiftSummary(
    Guid ShiftId,
    string FolioText,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    bool IsOpen,
    long OpeningFloatCents,
    int SalesCount,
    long TotalSoldCents,
    long DepositsCents,
    long WithdrawalsCents,
    long? ExpectedCashCents,
    long? CountedCashCents,
    long? DifferenceCents,
    IReadOnlyList<MyShiftMovement> Movements);

/// <summary>Turno propio en la lista de los más recientes.</summary>
public sealed record MyShiftListItem(Guid ShiftId, string FolioText, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, bool IsOpen, long TotalSoldCents);
