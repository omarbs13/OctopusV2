using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.SearchSuppliers;

/// <summary>Lista de proveedores de 100 en 100, por nombre o RFC normalizados (FR-004).</summary>
public sealed class SearchSuppliersHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;

    public SearchSuppliersHandler(IAccessControl access, ISupplierRepository suppliers)
    {
        _access = access;
        _suppliers = suppliers;
    }

    public async Task<Result<SupplierPage>> HandleAsync(SearchSuppliersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageSuppliers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<SupplierPage>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        var page = await _suppliers.SearchAsync(
            new SupplierSearch(text, query.IncludeInactive, Math.Max(query.Page, 1), SupplierPage.DefaultPageSize),
            cancellationToken);
        return Result.Success(page);
    }
}
