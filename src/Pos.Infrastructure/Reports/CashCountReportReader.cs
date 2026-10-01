using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Turnos del período para el arqueo (research §1, data-model.md). Los cerrados salen de la instantánea
/// del cierre; los abiertos se calculan con las ventas y movimientos vigentes y no traen efectivo
/// esperado, contado ni diferencia.
/// </summary>
internal sealed class CashCountReportReader : ICashCountReportReader
{
    private readonly PosDbContext _context;

    public CashCountReportReader(PosDbContext context) => _context = context;

    public async Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var shifts = _context.CashShifts.AsNoTracking()
            .Where(s => s.OpenedAt >= window.FromUtc && s.OpenedAt < window.ToUtcExclusive);
        if (cashierId is { } id)
        {
            shifts = shifts.Where(s => s.OpenedBy == id);
        }

        var list = await (
                from s in shifts
                join u in _context.Users.AsNoTracking() on s.OpenedBy equals u.Id
                orderby s.OpenedAt, s.Number
                select new
                {
                    s.Id,
                    s.Number,
                    CashierName = u.FullName,
                    s.Status,
                    s.OpenedAt,
                    s.ClosedAt,
                    s.OpeningFloatCents,
                    s.TotalSoldCents,
                    s.DepositsCents,
                    s.WithdrawalsCents,
                    s.ExpectedCashCents,
                    s.CountedCashCents,
                    s.DifferenceCents,
                })
            .ToListAsync(cancellationToken);

        var openIds = list.Where(s => s.Status == CashShiftStatus.Open).Select(s => s.Id).ToList();
        var sold = openIds.Count == 0
            ? []
            : await _context.Sales.AsNoTracking()
                .Where(s => s.CashShiftId != null && openIds.Contains(s.CashShiftId.Value) && s.Status == SaleStatus.Completed)
                .GroupBy(s => s.CashShiftId!.Value)
                .Select(g => new { ShiftId = g.Key, Cents = g.Sum(s => s.TotalCents) })
                .ToDictionaryAsync(x => x.ShiftId, x => x.Cents, cancellationToken);
        var movements = openIds.Count == 0
            ? []
            : await _context.CashMovements.AsNoTracking()
                .Where(m => openIds.Contains(m.CashShiftId))
                .GroupBy(m => new { m.CashShiftId, m.Type })
                .Select(g => new { g.Key.CashShiftId, g.Key.Type, Cents = g.Sum(m => m.AmountCents) })
                .ToListAsync(cancellationToken);

        return [.. list.Select(s =>
        {
            var isOpen = s.Status == CashShiftStatus.Open;
            return new CashCountRawRow(
                s.Id,
                ShiftFolio.Format(s.Number),
                s.CashierName,
                s.OpenedAt,
                s.ClosedAt,
                s.OpeningFloatCents,
                isOpen ? sold.GetValueOrDefault(s.Id) : s.TotalSoldCents ?? 0,
                isOpen ? movements.Where(m => m.CashShiftId == s.Id && m.Type == CashMovementType.In).Sum(m => m.Cents) : s.DepositsCents ?? 0,
                isOpen ? movements.Where(m => m.CashShiftId == s.Id && m.Type == CashMovementType.Out).Sum(m => m.Cents) : s.WithdrawalsCents ?? 0,
                isOpen,
                isOpen ? null : s.ExpectedCashCents,
                isOpen ? null : s.CountedCashCents,
                isOpen ? null : s.DifferenceCents);
        })];
    }
}
