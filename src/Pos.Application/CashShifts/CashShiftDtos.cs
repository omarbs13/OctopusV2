using Pos.Domain.CashShifts;
using Pos.Domain.Sales;

namespace Pos.Application.CashShifts;

/// <summary>
/// Resumen del turno abierto para el Punto de venta y Inicio. Nunca trae fondo, esperado, desglose
/// por forma de pago ni movimientos (FR-022).
/// </summary>
public sealed record CurrentShiftSummary(
    Guid ShiftId,
    int Version,
    string Folio,
    DateTime OpenedAtUtc,
    Guid OpenedById,
    string OpenedByName,
    bool IsMine,
    int SalesCount,
    long TotalSoldCents);

/// <summary>Criterios de "Turnos" ya normalizados; el límite superior de fecha es exclusivo (UTC).</summary>
public sealed record ShiftSearch(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,
    CashShiftStatus? Status,
    int Page,
    int PageSize);

public sealed record ShiftListItemDto(
    Guid Id,
    string Folio,
    string OpenedByName,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    CashShiftStatus Status,
    long TotalSoldCents,
    long? DifferenceCents);

public sealed record ShiftPage(IReadOnlyList<ShiftListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Venta de un turno en su detalle.</summary>
public sealed record ShiftSaleRowDto(
    Guid SaleId,
    string Folio,
    DateTime CreatedAtUtc,
    long TotalCents,
    IReadOnlyList<PaymentMethod> Methods,
    SaleStatus Status);

public sealed record CashMovementDto(
    Guid Id,
    string Folio,
    int Sequence,
    DateTime CreatedAtUtc,
    CashMovementType Type,
    long AmountCents,
    string Reason,
    string CreatedByName,
    string? AuthorizedByName);

/// <summary>
/// Arqueo de un turno. <c>IsSnapshot</c> indica que viene de la instantánea del cierre; si no, es el
/// esperado calculado al momento y no hay conteo.
/// </summary>
public sealed record ShiftReconciliationDto(
    bool IsSnapshot,
    int SalesCount,
    int CancelledCount,
    long TotalSoldCents,
    long CashSalesCents,
    long CashCancelledCents,
    long CardCents,
    long TransferCents,
    long DepositsCents,
    long WithdrawalsCents,
    long ExpectedCashCents,
    long? CountedCashCents,
    long? DifferenceCents,
    string? Comment,
    long CashRefundsCents = 0,
    long NonCashRefundsCents = 0,
    long CreditNotesIssuedCents = 0,
    ShiftCreditTotals? Credit = null);

/// <summary>
/// Bloque "Crédito" del turno (014, FR-012): ventas a crédito, abonos en efectivo y con tarjeta o
/// transferencia y anulaciones de abonos. Nulo en turnos cerrados antes de 0.9.0.
/// </summary>
public sealed record ShiftCreditTotals(
    long OnAccountSalesCents,
    long PaymentsCashCents,
    long PaymentsNonCashCents,
    long PaymentVoidsCashCents,
    long PaymentVoidsNonCashCents)
{
    public long PaymentVoidsCents => PaymentVoidsCashCents + PaymentVoidsNonCashCents;

    public static ShiftCreditTotals From(ShiftSalesTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        return new(
            totals.OnAccountSalesCents,
            totals.CustomerPaymentsCashCents,
            totals.CustomerPaymentsNonCashCents,
            totals.CustomerPaymentVoidsCashCents,
            totals.CustomerPaymentVoidsNonCashCents);
    }

    /// <summary>Instantánea del cierre; nula si el turno se cerró antes de 0.9.0.</summary>
    public static ShiftCreditTotals? From(CashShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        return shift.OnAccountSalesCents is null
            ? null
            : new(
                shift.OnAccountSalesCents ?? 0,
                shift.CustomerPaymentsCashCents ?? 0,
                shift.CustomerPaymentsNonCashCents ?? 0,
                shift.CustomerPaymentVoidsCashCents ?? 0,
                shift.CustomerPaymentVoidsNonCashCents ?? 0);
    }
}

public sealed record ShiftDetailDto(
    Guid Id,
    int Version,
    string Folio,
    CashShiftStatus Status,
    string OpenedByName,
    DateTime OpenedAtUtc,
    long OpeningFloatCents,
    DateTime? ClosedAtUtc,
    string? ClosedByName,
    IReadOnlyList<ShiftSaleRowDto> Sales,
    IReadOnlyList<CashMovementDto> Movements,
    ShiftReconciliationDto Reconciliation,
    string? CutFolio = null);

/// <summary>Cifras que se revelan solo después de capturar el conteo (research §8).</summary>
public sealed record ShiftCountResult(
    long ExpectedCents,
    long CountedCents,
    long DifferenceCents,
    DifferenceKind DifferenceKind,
    long CardCents,
    long TransferCents,
    int Version);

public sealed record RegisteredMovement(Guid MovementId, string Folio);

/// <summary>Turno cerrado con su Corte Z (017): <c>CutFolio</c> es <c>Z-000001</c>.</summary>
public sealed record ClosedShift(Guid ShiftId, string Folio, Guid CutId, string CutFolio);

/// <summary>
/// Datos del corte: solo los de FR-019, leídos de la instantánea del turno cerrado. <c>CutFolio</c> es
/// el folio de su Corte Z (017); nulo en turnos cerrados antes de 0.12.0.
/// </summary>
public sealed record ShiftReportDto(
    Guid ShiftId,
    string Folio,
    string RegisterName,
    Guid OpenedById,
    string OpenedByName,
    Guid ClosedById,
    string ClosedByName,
    DateTime OpenedAtUtc,
    DateTime ClosedAtUtc,
    long OpeningFloatCents,
    int SalesCount,
    int CancelledCount,
    long CashSalesCents,
    long CashCancelledCents,
    long CardCents,
    long TransferCents,
    long DepositsCents,
    long WithdrawalsCents,
    long ExpectedCashCents,
    long CountedCashCents,
    long DifferenceCents,
    string? Comment,
    long CashRefundsCents = 0,
    long NonCashRefundsCents = 0,
    long CreditNotesIssuedCents = 0,
    ShiftCreditTotals? Credit = null,
    string? CutFolio = null);

/// <summary>Datos del comprobante de un movimiento de efectivo, más lo necesario para decidir el acceso.</summary>
public sealed record CashMovementReceiptDto(
    Guid MovementId,
    string Folio,
    CashMovementType Type,
    long AmountCents,
    string Reason,
    DateTime CreatedAtUtc,
    string CreatedByName,
    Guid CreatedById,
    Guid ShiftOwnerId,
    string? AuthorizedByName);
