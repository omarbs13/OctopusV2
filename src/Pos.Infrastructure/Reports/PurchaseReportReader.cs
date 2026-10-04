using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// "Reportes > Compras" (020, research §13): la página ordenada por fecha de factura, fecha de registro e id
/// (índice <c>IX_Purchases_InvoiceDate</c>) y los acumulados de las vigentes en una sola consulta agregada.
/// </summary>
public sealed class PurchaseReportReader : IPurchaseReportReader
{
    private readonly PosDbContext _context;

    public PurchaseReportReader(PosDbContext context) => _context = context;

    public async Task<PurchaseReportPage> SearchAsync(PurchaseReportFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var purchases = _context.Purchases.AsNoTracking();
        if (filter.SupplierId is { } supplierId)
        {
            purchases = purchases.Where(p => p.SupplierId == supplierId);
        }

        if (filter.FromDate is { } from)
        {
            purchases = purchases.Where(p => p.InvoiceDate >= from);
        }

        if (filter.ToDate is { } to)
        {
            purchases = purchases.Where(p => p.InvoiceDate <= to);
        }

        if (filter.MinTotalCents is { } min)
        {
            purchases = purchases.Where(p => p.TotalCents >= min);
        }

        if (filter.MaxTotalCents is { } max)
        {
            purchases = purchases.Where(p => p.TotalCents <= max);
        }

        var listed = filter.IncludeVoided ? purchases : purchases.Where(p => p.Status == PurchaseStatus.Active);

        // FR-021, FR-021a: los acumulados cuentan solo las vigentes de todo el filtro, no solo de la página.
        var sums = await purchases
            .Where(p => p.Status == PurchaseStatus.Active)
            .GroupBy(p => 1)
            .Select(g => new
            {
                Count = g.LongCount(),
                Subtotal = g.Sum(p => p.SubtotalCents),
                Tax = g.Sum(p => p.TaxCents),
                Total = g.Sum(p => p.TotalCents),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalCount = await listed.LongCountAsync(cancellationToken);
        var pageCount = totalCount <= 0 ? 1 : (int)((totalCount + PurchaseReportPage.PageSize - 1) / PurchaseReportPage.PageSize);
        var page = Math.Clamp(filter.Page, 1, pageCount);

        var rows = await (
                from p in listed
                join author in _context.Users on p.CreatedBy equals author.Id into authors
                from author in authors.DefaultIfEmpty()
                orderby p.InvoiceDate descending, p.CreatedAt descending, p.Id descending
                select new
                {
                    p.Id,
                    p.InvoiceDate,
                    p.SupplierName,
                    p.InvoiceNumber,
                    p.LineCount,
                    p.SubtotalCents,
                    p.TaxCents,
                    p.TotalCents,
                    p.CreatedBy,
                    AuthorName = author == null ? null : author.UserName,
                    p.Status,
                })
            .Skip((page - 1) * PurchaseReportPage.PageSize)
            .Take(PurchaseReportPage.PageSize)
            .ToListAsync(cancellationToken);

        return new PurchaseReportPage(
            [.. rows.Select(r => new PurchaseReportRow(
                r.Id,
                r.InvoiceDate,
                r.SupplierName,
                r.InvoiceNumber,
                r.LineCount,
                r.SubtotalCents,
                r.TaxCents,
                r.TotalCents,
                r.AuthorName ?? SystemUser.NameOf(r.CreatedBy),
                r.Status == PurchaseStatus.Voided))],
            totalCount,
            page,
            sums?.Count ?? 0,
            sums?.Subtotal ?? 0,
            sums?.Tax ?? 0,
            sums?.Total ?? 0);
    }
}
