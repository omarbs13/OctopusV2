using Pos.Domain.Reports;

namespace Pos.Application.Reports.GetCashCountReport;

public sealed record CashCountReportQuery(ReportPeriod Period, Guid? CashierId = null);

/// <summary>
/// Turno tal como lo lee Infrastructure: sin porcentaje ni alerta, que calcula el caso de uso. Para un
/// turno abierto el efectivo esperado, contado y diferencia son siempre nulos.
/// </summary>
public sealed record CashCountRawRow(
    Guid ShiftId,
    string FolioText,
    string CashierName,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    long OpeningFloatCents,
    long TotalSoldCents,
    long DepositsCents,
    long WithdrawalsCents,
    bool IsOpen,
    long? ExpectedCashCents,
    long? CountedCashCents,
    long? DifferenceCents);

/// <summary>Fila del arqueo. Los cinco campos de efectivo de un turno abierto ("En curso") son nulos y <c>IsAlert</c> es falso.</summary>
public sealed record CashCountRow(
    Guid ShiftId,
    string FolioText,
    string CashierName,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    long OpeningFloatCents,
    long TotalSoldCents,
    long DepositsCents,
    long WithdrawalsCents,
    bool IsOpen,
    long? ExpectedCashCents,
    long? CountedCashCents,
    long? DifferenceCents,
    long? DifferenceBasisPoints,
    bool IsAlert);

/// <summary>Totales del período: turnos cerrados, total vendido de todos los turnos y diferencia acumulada de los cerrados.</summary>
public sealed record CashCountTotals(int ClosedShifts, long TotalSoldCents, long AccumulatedDifferenceCents);

public sealed record CashCountReport(
    IReadOnlyList<CashCountRow> Rows,
    CashCountTotals Totals,
    long ThresholdBasisPoints)
{
    public int AlertCount => Rows.Count(r => r.IsAlert);
}
