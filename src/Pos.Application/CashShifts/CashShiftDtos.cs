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
    string? Comment);

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
    ShiftReconciliationDto Reconciliation);

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

public sealed record ClosedShift(Guid ShiftId, string Folio);

/// <summary>Datos del corte: solo los de FR-019, leídos de la instantánea del turno cerrado.</summary>
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
    string? Comment);

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
