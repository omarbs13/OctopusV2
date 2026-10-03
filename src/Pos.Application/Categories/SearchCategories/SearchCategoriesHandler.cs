using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Categories.SearchCategories;

/// <summary>Catálogo de categorías ordenado por nombre, con su número de productos (FR-001).</summary>
public sealed class SearchCategoriesHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;

    public SearchCategoriesHandler(IAccessControl access, ICategoryRepository categories)
    {
        _access = access;
        _categories = categories;
    }

    public async Task<Result<IReadOnlyList<CategoryListItemDto>>> HandleAsync(SearchCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCategories, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<CategoryListItemDto>>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        return Result.Success(await _categories.SearchAsync(text, query.Status, cancellationToken));
    }
}
