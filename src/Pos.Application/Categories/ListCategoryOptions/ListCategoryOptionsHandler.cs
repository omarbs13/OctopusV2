using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Categories.ListCategoryOptions;

/// <summary>Categorías no borradas para selectores y filtros, ordenadas por nombre (permiso <c>ViewProducts</c>).</summary>
public sealed class ListCategoryOptionsHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;

    public ListCategoryOptionsHandler(IAccessControl access, ICategoryRepository categories)
    {
        _access = access;
        _categories = categories;
    }

    public async Task<Result<IReadOnlyList<CategoryOptionDto>>> HandleAsync(ListCategoryOptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<CategoryOptionDto>>(access.Error!);
        }

        return Result.Success(await _categories.ListOptionsAsync(query.IncludeInactive, cancellationToken));
    }
}
