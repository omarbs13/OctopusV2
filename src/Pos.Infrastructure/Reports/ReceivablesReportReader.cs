using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Domain.Receivables;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Saldos por cliente para "Reportes > Créditos": agrupa las cuentas <c>PENDING</c> por cliente con el
/// índice (<c>CustomerId</c>, <c>Status</c>, <c>CreatedAt</c>) y lee el último abono vigente por lote.
/// </summary>
public sealed class ReceivablesReportReader : IReceivablesReportReader
{
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public ReceivablesReportReader(PosDbContext context) => _context = context;

    public async Task<IReadOnlyList<ReceivablesReportSource>> GetAsync(string? text, CancellationToken cancellationToken)
    {
        var balances = _context.Receivables.AsNoTracking()
            .Where(r => r.Status == ReceivableStatus.Pending)
            .GroupBy(r => r.CustomerId)
            .Select(g => new { CustomerId = g.Key, Balance = g.Sum(r => r.BalanceCents), Oldest = g.Min(r => r.CreatedAt) })
            .Where(g => g.Balance > 0);

        var customers = _context.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(text))
        {
            var pattern = "%" + text.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";
            customers = customers.Where(c => EF.Functions.Like(c.SearchText, pattern, LikeEscape));
        }

        var rows = await (
                from b in balances
                join c in customers on b.CustomerId equals c.Id
                select new { c.Id, c.Name, b.Balance, c.CreditLimitCents, b.Oldest })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var lastPayments = ids.Count == 0
            ? []
            : await _context.CustomerPayments.AsNoTracking()
                .Where(p => ids.Contains(p.CustomerId) && p.Status == CustomerPaymentStatus.Active)
                .GroupBy(p => p.CustomerId)
                .Select(g => new { CustomerId = g.Key, Last = g.Max(p => p.CreatedAt) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Last, cancellationToken);

        return [.. rows.Select(r => new ReceivablesReportSource(
            r.Id,
            r.Name,
            r.Balance,
            r.CreditLimitCents,
            r.Oldest,
            lastPayments.TryGetValue(r.Id, out var last) ? last : null))];
    }
}
