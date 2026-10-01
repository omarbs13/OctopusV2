using Pos.Application.Abstractions;
using Pos.Application.Receivables;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Receivables;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetReceivablesReport;

/// <summary>
/// "Reportes > Créditos" (<c>ViewReceivables</c>, licencia Crédito y clientes): saldo, límite, último
/// abono y días vencido por cliente con saldo, con el plazo actual. Los totales se calculan sobre las
/// filas filtradas y no hay caché: siempre refleja lo último registrado (Historia 4).
/// </summary>
public sealed class GetReceivablesReportHandler
{
    private readonly IAccessControl _access;
    private readonly IReceivablesReportReader _reader;
    private readonly CreditAging _aging;

    public GetReceivablesReportHandler(IAccessControl access, IReceivablesReportReader reader, CreditAging aging)
    {
        _access = access;
        _reader = reader;
        _aging = aging;
    }

    public async Task<Result<ReceivablesReport>> HandleAsync(GetReceivablesReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewReceivables, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReceivablesReport>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        var aging = _aging.Now();
        var rows = (await _reader.GetAsync(text, cancellationToken))
            .Select(source =>
            {
                var days = aging.DaysOverdue(source.OldestPendingUtc);
                return new ReceivablesReportRow(
                    source.CustomerId,
                    source.Name,
                    source.BalanceCents,
                    source.LimitCents,
                    source.LastPaymentAtUtc,
                    days,
                    ReceivableAging.IsOverdue(days, source.BalanceCents),
                    source.BalanceCents >= source.LimitCents);
            })
            .Where(row => query.Status switch
            {
                ReceivablesStatusFilter.Current => !row.IsOverdue,
                ReceivablesStatusFilter.Overdue => row.IsOverdue,
                ReceivablesStatusFilter.AtLimit => row.IsAtLimit,
                _ => true,
            })
            .OrderByDescending(row => row.DaysOverdue)
            .ThenByDescending(row => row.BalanceCents)
            .ThenBy(row => row.Name, StringComparer.CurrentCulture)
            .ToList();
        return Result.Success(new ReceivablesReport(rows, rows.Sum(r => r.BalanceCents), rows.Count, aging.TermDays));
    }
}
