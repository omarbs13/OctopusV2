using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Categories.GetCategory;

/// <summary>Categoría no borrada con su número de productos, para el formulario.</summary>
public sealed class GetCategoryHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;

    public GetCategoryHandler(IAccessControl access, ICategoryRepository categories)
    {
        _access = access;
        _categories = categories;
    }

    public async Task<Result<CategoryDto>> HandleAsync(GetCategoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCategories, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CategoryDto>(access.Error!);
        }

        var category = await _categories.GetAsync(query.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<CategoryDto>(new NotFound());
        }

        var count = await _categories.CountProductsAsync(category.Id, cancellationToken);
        return Result.Success(new CategoryDto(category.Id, category.Name, category.Description, category.IsActive, count, category.Version));
    }
}
