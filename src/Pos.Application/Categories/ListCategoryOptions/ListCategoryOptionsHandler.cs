using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Categories.ListCategoryOptions;

/// <summary>
/// Categorías no borradas para selectores y filtros, ordenadas por nombre (permiso <c>ViewProducts</c>). Con el
/// módulo Categorías inactivo devuelve una lista vacía: el selector desaparece y las categorías se conservan (025, FR-007).
/// </summary>
public sealed class ListCategoryOptionsHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;
    private readonly ILicenseState? _license;

    public ListCategoryOptionsHandler(IAccessControl access, ICategoryRepository categories, ILicenseState? license = null)
    {
        _access = access;
        _categories = categories;
        _license = license;
    }

    public async Task<Result<IReadOnlyList<CategoryOptionDto>>> HandleAsync(ListCategoryOptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<CategoryOptionDto>>(access.Error!);
        }

        if (_license?.IsModuleActive(LicensedModule.Categories) == false)
        {
            return Result.Success<IReadOnlyList<CategoryOptionDto>>([]);
        }

        return Result.Success(await _categories.ListOptionsAsync(query.IncludeInactive, cancellationToken));
    }
}
