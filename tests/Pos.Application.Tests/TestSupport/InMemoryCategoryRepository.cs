using Pos.Application.Categories;
using Pos.Application.Products;
using Pos.Domain.Categories;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Doble mínimo de <see cref="ICategoryRepository"/> para las pruebas de productos (016).</summary>
public sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly Dictionary<Guid, Category> _categories = [];

    public Category Seed(Category category)
    {
        _categories[category.Id] = category;
        return category;
    }

    public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.TryGetValue(id, out var category) && !category.IsDeleted ? category : null);

    public Task<bool> NameExistsAsync(string nameKey, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.Values.Any(c => !c.IsDeleted && c.NameKey == nameKey && c.Id != excludingId));

    public Task<int> CountProductsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<IReadOnlyList<CategoryListItemDto>> SearchAsync(string? nameKey, CategoryStatusFilter status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryListItemDto>>([.. Live()
            .Where(c => (nameKey is null || c.NameKey.Contains(nameKey, StringComparison.Ordinal))
                && (status == CategoryStatusFilter.All || c.IsActive == (status == CategoryStatusFilter.Active)))
            .Select(c => new CategoryListItemDto(c.Id, c.Name, c.Description, c.IsActive, 0, c.Version))]);

    public Task<IReadOnlyList<CategoryOptionDto>> ListOptionsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryOptionDto>>([.. Live()
            .Where(c => includeInactive || c.IsActive)
            .Select(c => new CategoryOptionDto(c.Id, c.Name, c.IsActive))]);

    public Task ClearFromDeletedProductsAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Add(Category category) => Seed(category);

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(SaveOutcome.Saved);

    private IEnumerable<Category> Live() =>
        _categories.Values.Where(c => !c.IsDeleted).OrderBy(c => c.NameKey, StringComparer.Ordinal);
}
