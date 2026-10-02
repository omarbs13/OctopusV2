using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Categories;
using Pos.Application.Products;
using Pos.Domain.Categories;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Categories;

public sealed class CategoryRepository : ICategoryRepository
{
    // https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public CategoryRepository(PosDbContext context) => _context = context;

    public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categories.SingleOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);

    public Task<bool> NameExistsAsync(string nameKey, Guid? excludingId, CancellationToken cancellationToken) =>
        _context.Categories.AsNoTracking()
            .AnyAsync(c => c.NameKey == nameKey && c.DeletedAt == null && (excludingId == null || c.Id != excludingId), cancellationToken);

    public Task<int> CountProductsAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products.AsNoTracking().CountAsync(p => p.CategoryId == id && p.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<CategoryListItemDto>> SearchAsync(string? nameKey, CategoryStatusFilter status, CancellationToken cancellationToken)
    {
        var categories = _context.Categories.AsNoTracking().Where(c => c.DeletedAt == null);
        categories = status switch
        {
            CategoryStatusFilter.Active => categories.Where(c => c.IsActive),
            CategoryStatusFilter.Inactive => categories.Where(c => !c.IsActive),
            _ => categories,
        };

        if (!string.IsNullOrEmpty(nameKey))
        {
            var pattern = LikeContains(nameKey);
            categories = categories.Where(c => EF.Functions.Like(c.NameKey, pattern, LikeEscape));
        }

        return await categories
            .OrderBy(c => c.NameKey)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsActive,
                _context.Products.Count(p => p.CategoryId == c.Id && p.DeletedAt == null),
                c.Version))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryOptionDto>> ListOptionsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await _context.Categories.AsNoTracking()
            .Where(c => c.DeletedAt == null && (includeInactive || c.IsActive))
            .OrderBy(c => c.NameKey)
            .Select(c => new CategoryOptionDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(cancellationToken);

    public Task ClearFromDeletedProductsAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products
            .Where(p => p.CategoryId == id && p.DeletedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CategoryId, (Guid?)null), cancellationToken);

    public void Add(Category category) => _context.Categories.Add(category);

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
            return unique.Message.Contains("Categories.NameKey", StringComparison.Ordinal)
                ? SaveOutcome.Duplicate(CategoryFields.Name)
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
