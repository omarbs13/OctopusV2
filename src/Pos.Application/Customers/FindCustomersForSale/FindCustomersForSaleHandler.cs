using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Customers.FindCustomersForSale;

/// <summary>Hasta 20 clientes activos con crédito para elegir en el cobro (<c>SellOnCredit</c>).</summary>
public sealed class FindCustomersForSaleHandler
{
    public const int Limit = 20;

    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;

    public FindCustomersForSaleHandler(IAccessControl access, ICustomerRepository customers)
    {
        _access = access;
        _customers = customers;
    }

    public async Task<Result<IReadOnlyList<CustomerForSaleDto>>> HandleAsync(FindCustomersForSaleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.SellOnCredit, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<CustomerForSaleDto>>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? string.Empty : TextNormalizer.ForSearch(query.Text.Trim());
        var customers = await _customers.FindForSaleAsync(text, Limit, cancellationToken);
        return Result.Success<IReadOnlyList<CustomerForSaleDto>>(
            [.. customers.Select(c => new CustomerForSaleDto(c.Id, c.Name, c.Phone, c.TaxId))]);
    }
}
