using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts;

/// <summary>Renglón del "Histórico de cortes" (FR-015); <c>DifferenceCents</c> solo en los Z.</summary>
public sealed record ShiftCutListItemDto(
    Guid Id,
    ShiftCutType Type,
    string Folio,
    DateTime GeneratedAtUtc,
    string GeneratedByName,
    string ShiftFolio,
    long TotalSoldCents,
    long? DifferenceCents);

public sealed record ShiftCutPage(IReadOnlyList<ShiftCutListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Criterios del histórico ya normalizados; <c>FromUtc</c> inclusivo y <c>ToUtcExclusive</c> exclusivo.</summary>
public sealed record ShiftCutSearch(
    ShiftCutType? Type,
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,
    int Page,
    int PageSize);

/// <summary>Reporte fijo de un corte: lo que se muestra e imprime (FR-004, FR-014).</summary>
public sealed record ShiftCutReportDto(
    Guid CutId,
    ShiftCutType Type,
    string Folio,
    string ShiftFolio,
    string RegisterName,
    Guid GeneratedById,
    string GeneratedByName,
    string? AuthorizedByName,
    Guid ShiftOwnerId,
    string ShiftOwnerName,
    DateTime ShiftOpenedAtUtc,
    DateTime GeneratedAtUtc,
    long OpeningFloatCents,
    int SalesCount,
    int CancelledCount,
    long TotalSoldCents,
    long CashSalesCents,
    long CashCancelledCents,
    long CardCents,
    long TransferCents,
    long CashRefundsCents,
    long NonCashRefundsCents,
    long CreditNotesIssuedCents,
    ShiftCreditTotals Credit,
    long DepositsCents,
    long WithdrawalsCents,
    long ExpectedCashCents,
    long? CountedCashCents,
    long? DifferenceCents,
    string? Comment);
