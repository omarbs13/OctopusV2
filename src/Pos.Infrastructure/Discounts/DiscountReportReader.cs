using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Reports;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Discounts;

/// <summary>
/// Reporte de descuentos: <c>SaleDiscounts</c> unido a las ventas completadas del período (015, research §12).
/// El total sale de la misma consulta que la tabla, así que coinciden (SC-003).
/// </summary>
internal sealed class DiscountReportReader : IDiscountReportReader
{
    private readonly PosDbContext _context;

    public DiscountReportReader(PosDbContext context) => _context = context;

    public async Task<DiscountReport> ReadAsync(DiscountReportQuery query, ReportWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(window);

        var discounts =
            from d in _context.SaleDiscounts.AsNoTracking()
            join s in _context.Sales.AsNoTracking() on d.SaleId equals s.Id
            where s.Status == SaleStatus.Completed && d.CreatedAt >= window.FromUtc && d.CreatedAt < window.ToUtcExclusive
            select new { Discount = d, Sale = s };
        if (query.CashierId is { } cashierId)
        {
            discounts = discounts.Where(x => x.Sale.CreatedBy == cashierId);
        }

        if (query.Kind is { } kind)
        {
            discounts = discounts.Where(x => x.Discount.Kind == kind);
        }

        var count = await discounts.CountAsync(cancellationToken);
        var total = count == 0 ? 0 : await discounts.SumAsync(x => x.Discount.AmountCents, cancellationToken);
        var pageCount = count <= 0 ? 1 : (int)((count + (long)query.PageSize - 1) / query.PageSize);
        var page = Math.Clamp(query.Page, 1, pageCount);

        var rows = await (
                from x in discounts
                join cashier in _context.Users on x.Sale.CreatedBy equals cashier.Id into cashiers
                from cashier in cashiers.DefaultIfEmpty()
                join authorizer in _context.Users on x.Discount.AuthorizedBy equals authorizer.Id into authorizers
                from authorizer in authorizers.DefaultIfEmpty()
                orderby x.Discount.CreatedAt descending, x.Discount.Id
                select new
                {
                    x.Sale.Id,
                    x.Sale.FolioNumber,
                    x.Discount.CreatedAt,
                    x.Sale.CreatedBy,
                    CashierName = cashier == null ? null : cashier.UserName,
                    x.Discount.Kind,
                    x.Discount.Mode,
                    x.Discount.Value,
                    x.Discount.AmountCents,
                    AuthorizedByName = authorizer == null ? null : authorizer.UserName,
                    x.Discount.CouponCode,
                })
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new DiscountReport(
            total,
            count,
            [.. rows.Select(r => new DiscountReportRow(
                r.Id,
                Folio.Format(r.FolioNumber),
                r.CreatedAt,
                r.CashierName ?? SystemUser.NameOf(r.CreatedBy),
                r.Kind,
                r.Mode,
                r.Value,
                r.AmountCents,
                r.AuthorizedByName,
                r.CouponCode))],
            count,
            page,
            query.PageSize);
    }
}
