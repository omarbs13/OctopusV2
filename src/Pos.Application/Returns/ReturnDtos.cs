using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Application.Returns;

/// <summary>Resultado de una cancelación o devolución confirmada.</summary>
public sealed record ReturnResult(Guid ReturnId, string Folio, long TotalCents, Guid? CreditNoteId, string? CreditNoteFolio)
{
    /// <summary>Resultado de la cancelación básica sin Devoluciones (sin registro de devolución).</summary>
    public static ReturnResult Basic(long totalCents) => new(Guid.Empty, string.Empty, totalCents, null, null);
}

public sealed record ReturnPreviewLine(Guid SaleLineId, long QuantityThousandths, long AmountCents);

public sealed record RefundBreakdownItem(PaymentMethod Method, long AmountCents);

/// <summary>
/// Vista previa de una devolución, calculada con <c>ReturnMath</c>. <c>CanRefundCash</c> es falso sin
/// turno abierto utilizable o con efectivo insuficiente, sin revelar montos.
/// </summary>
public sealed record ReturnPreview(
    long TotalCents,
    IReadOnlyList<ReturnPreviewLine> Lines,
    IReadOnlyList<RefundBreakdownItem> RefundBreakdown,
    long CashRefundCents,
    bool WithinWindow,
    bool CanRefundCash,
    int WindowDays);

/// <summary>Evento de devolución en el historial de una venta (FR-013).</summary>
public sealed record ReturnSummaryDto(
    Guid ReturnId,
    string Folio,
    DateTime CreatedAtUtc,
    string CreatedByName,
    string Reason,
    string AuthorizedByName,
    long TotalCents,
    ReturnKind Kind,
    ReturnCompensation Compensation,
    string? CreditNoteFolio);

public enum ReversalFilter
{
    Pending,
    Reversed,
    All,
}

public sealed record ReversalSearch(ReversalFilter Filter, int Page, int PageSize);

public sealed record PendingReversalDto(
    Guid RefundId,
    string ReturnFolio,
    string SaleFolio,
    PaymentMethod Method,
    long AmountCents,
    DateTime CreatedAtUtc,
    RefundStatus Status,
    string? ReversedByName);

public sealed record ReversalPage(IReadOnlyList<PendingReversalDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}
