using Pos.Domain.Receivables;
using Pos.Domain.Sales;

namespace Pos.Application.Receivables;

/// <summary>Resultado de un abono registrado: folio, saldos del cliente y cuentas que quedaron pagadas.</summary>
public sealed record PaymentReceipt(
    Guid PaymentId,
    string Folio,
    long BalanceBeforeCents,
    long BalanceAfterCents,
    IReadOnlyList<Guid> PaidReceivables);

public sealed record CustomerPaymentRowDto(
    Guid PaymentId,
    string Folio,
    DateTime CreatedAtUtc,
    long AmountCents,
    PaymentMethod Method,
    string? Reference,
    long BalanceBeforeCents,
    long BalanceAfterCents,
    CustomerPaymentStatus Status,
    string? VoidReason);

public sealed record CustomerPaymentPage(IReadOnlyList<CustomerPaymentRowDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

public sealed record ReceivableRowDto(
    Guid ReceivableId,
    Guid SaleId,
    string SaleFolio,
    DateTime SaleDateUtc,
    long OriginalCents,
    long BalanceCents,
    ReceivableStatus Status,
    int DaysOverdue);

public sealed record ReceivablePage(IReadOnlyList<ReceivableRowDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Datos del recibo de abono (FR-013); <c>IsVoided</c> imprime la leyenda "ANULADO".</summary>
public sealed record CustomerPaymentReceiptData(
    string Folio,
    DateTime CreatedAtUtc,
    string CustomerName,
    long AmountCents,
    PaymentMethod Method,
    string? Reference,
    long BalanceBeforeCents,
    long BalanceAfterCents,
    bool IsVoided);
