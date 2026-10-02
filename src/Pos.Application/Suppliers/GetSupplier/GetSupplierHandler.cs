using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.GetSupplier;

/// <summary>Ficha del proveedor para editarlo (<c>ManageSuppliers</c>).</summary>
public sealed class GetSupplierHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;

    public GetSupplierHandler(IAccessControl access, ISupplierRepository suppliers)
    {
        _access = access;
        _suppliers = suppliers;
    }

    public async Task<Result<SupplierDto>> HandleAsync(GetSupplierQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageSuppliers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<SupplierDto>(access.Error!);
        }

        var s = await _suppliers.GetAsync(query.Id, cancellationToken);
        return s is null
            ? Result.Failure<SupplierDto>(new NotFound())
            : Result.Success(new SupplierDto(s.Id, s.Version, s.Name, s.TaxId, s.Phone, s.Email, s.Address, s.PaymentTerms, s.CreditDays, s.IsActive));
    }
}
