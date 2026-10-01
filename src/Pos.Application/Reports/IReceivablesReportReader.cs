namespace Pos.Application.Reports;

/// <summary>Saldo de un cliente que debe, con la fecha de su cuenta pendiente más antigua y su último abono vigente.</summary>
public sealed record ReceivablesReportSource(
    Guid CustomerId,
    string Name,
    long BalanceCents,
    long LimitCents,
    DateTime OldestPendingUtc,
    DateTime? LastPaymentAtUtc);

/// <summary>
/// Lectura del reporte "Créditos" (014, research §13): una fila por cliente con saldo &gt; 0 en una sola
/// consulta agrupada con el índice (<c>CustomerId</c>, <c>Status</c>). El texto de búsqueda ya viene
/// normalizado (sin acentos y en minúsculas) o nulo.
/// </summary>
public interface IReceivablesReportReader
{
    Task<IReadOnlyList<ReceivablesReportSource>> GetAsync(string? text, CancellationToken cancellationToken);
}
