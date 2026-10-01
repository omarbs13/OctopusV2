using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Discounts;
using Pos.Application.Products;
using Pos.Domain.Discounts;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Discounts;

public sealed class CouponRepository : ICouponRepository
{
    // https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public CouponRepository(PosDbContext context) => _context = context;

    public Task<Coupon?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Coupons.SingleOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);

    public Task<Coupon?> FindByCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        _context.Coupons.SingleOrDefaultAsync(c => c.Code == normalizedCode && c.DeletedAt == null, cancellationToken);

    public void Add(Coupon coupon) => _context.Coupons.Add(coupon);

    public async Task<CouponPage> SearchAsync(CouponSearch search, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var coupons = _context.Coupons.AsNoTracking().Where(c => c.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = LikeContains(Coupon.NormalizeCode(search.Text));
            coupons = coupons.Where(c => EF.Functions.Like(c.Code, pattern, LikeEscape));
        }

        // Mismo orden que Coupon.StatusOn: inactivo > agotado > vencido > por iniciar > vigente.
        coupons = search.Status switch
        {
            CouponStatus.Inactive => coupons.Where(c => !c.IsActive),
            CouponStatus.Exhausted => coupons.Where(c => c.IsActive && c.UsageLimit != null && c.UsesCount >= c.UsageLimit),
            CouponStatus.Expired => coupons.Where(c => c.IsActive && !(c.UsageLimit != null && c.UsesCount >= c.UsageLimit)
                && c.EndsOn < today),
            CouponStatus.NotStarted => coupons.Where(c => c.IsActive && !(c.UsageLimit != null && c.UsesCount >= c.UsageLimit)
                && c.EndsOn >= today && c.StartsOn > today),
            CouponStatus.Active => coupons.Where(c => c.IsActive && !(c.UsageLimit != null && c.UsesCount >= c.UsageLimit)
                && c.EndsOn >= today && c.StartsOn <= today),
            _ => coupons,
        };

        var total = await coupons.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var items = await coupons
            .OrderBy(c => c.Code)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);
        var dtos = items
            .Select(c => new CouponListItemDto(
                c.Id, c.Code, c.Mode, c.Value, c.StartsOn, c.EndsOn, c.StatusOn(today), c.UsesCount, c.UsageLimit, c.IsActive, c.Version))
            .ToList();
        return new CouponPage(dtos, total, page, search.PageSize);
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
            return unique.Message.Contains("Coupons.Code", StringComparison.Ordinal)
                ? SaveOutcome.Duplicate(DiscountFields.Code)
                : SaveOutcome.Conflict;
        }
    }

    /// <summary>Patrón LIKE de "contiene", escapando los comodines del texto buscado.</summary>
    private static string LikeContains(string text)
    {
        var escaped = text
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
