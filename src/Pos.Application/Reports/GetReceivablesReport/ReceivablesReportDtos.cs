namespace Pos.Application.Reports.GetReceivablesReport;

/// <summary>Filtro de estado: "Al día" y "Vencido" se excluyen entre sí; "Al límite" es independiente (research §13).</summary>
public enum ReceivablesStatusFilter
{
    Current,
    Overdue,
    AtLimit,
}

public sealed record GetReceivablesReportQuery(ReceivablesStatusFilter? Status = null, string? Text = null);

public sealed record ReceivablesReportRow(
    Guid CustomerId,
    string Name,
    long BalanceCents,
    long LimitCents,
    DateTime? LastPaymentAtUtc,
    int DaysOverdue,
    bool IsOverdue,
    bool IsAtLimit);

/// <summary>Filas del reporte y totales calculados sobre las filas devueltas.</summary>
public sealed record ReceivablesReport(IReadOnlyList<ReceivablesReportRow> Rows, long TotalBalanceCents, int CustomerCount, int PaymentTermDays);
