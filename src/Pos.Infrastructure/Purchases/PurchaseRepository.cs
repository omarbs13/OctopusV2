using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Purchases;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Purchases;

public sealed class PurchaseRepository : IPurchaseRepository
{
    // https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public PurchaseRepository(PosDbContext context) => _context = context;

    public Task<Purchase?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Purchases.Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Purchase?> FindActiveByInvoiceAsync(Guid supplierId, string invoiceKey, CancellationToken cancellationToken) =>
        _context.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.InvoiceKey == invoiceKey && p.Status == PurchaseStatus.Active)
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(Purchase purchase) => _context.Purchases.Add(purchase);

    public async Task<PurchaseDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases.AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (purchase is null)
        {
            return null;
        }

        Guid[] userIds = purchase.VoidedBy is { } voidedBy ? [purchase.CreatedBy, voidedBy] : [purchase.CreatedBy];
        var names = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken);
        var units = await _context.UnitsOfMeasure.AsNoTracking().ToDictionaryAsync(u => u.Code, cancellationToken);

        return new PurchaseDetailDto(
            purchase.Id,
            purchase.Version,
            purchase.SupplierId,
            purchase.SupplierName,
            purchase.InvoiceNumber,
            purchase.InvoiceDate,
            purchase.CreatedAt,
            NameOf(names, purchase.CreatedBy),
            purchase.SubtotalCents,
            purchase.TaxCents,
            purchase.TotalCents,
            purchase.Status,
            purchase.VoidedAt,
            purchase.VoidedBy is { } by ? NameOf(names, by) : null,
            purchase.VoidReason,
            [.. purchase.Lines.OrderBy(l => l.LineNumber).Select(l =>
            {
                var unit = units.GetValueOrDefault(l.UnitCode);
                return new PurchaseLineDetailDto(
                    l.LineNumber,
                    l.ProductId,
                    l.ProductName,
                    l.ProductSku,
                    l.QuantityThousandths,
                    l.UnitCode,
                    unit?.Name ?? l.UnitCode,
                    unit?.DecimalPlaces ?? 3,
                    l.UnitCostCents,
                    l.AmountCents,
                    l.IsBonus);
            })]);
    }

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique } unique)
        {
            _context.ChangeTracker.Clear();
            return unique.Message.Contains("Purchases.SupplierId, Purchases.InvoiceKey", StringComparison.Ordinal)
                ? SaveOutcome.Duplicate(PurchaseFields.InvoiceNumber)
                : SaveOutcome.Conflict;
        }
    }

    private static string NameOf(Dictionary<Guid, string> names, Guid userId) =>
        names.GetValueOrDefault(userId) ?? SystemUser.NameOf(userId);
}
