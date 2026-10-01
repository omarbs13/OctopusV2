using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Customers.SearchCustomers;

/// <summary>Lista de clientes de 100 en 100 con su saldo, leído por lote en una sola consulta (FR-015).</summary>
public sealed class SearchCustomersHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;

    public SearchCustomersHandler(IAccessControl access, ICustomerRepository customers)
    {
        _access = access;
        _customers = customers;
    }

    public async Task<Result<CustomerPage>> HandleAsync(SearchCustomersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CustomerPage>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : TextNormalizer.ForSearch(query.Text.Trim());
        var page = await _customers.SearchAsync(
            new CustomerSearch(text, query.IncludeInactive, Math.Max(query.Page, 1), CustomerPage.DefaultPageSize),
            cancellationToken);
        var balances = await _customers.GetBalancesAsync([.. page.Items.Select(c => c.Id)], cancellationToken);
        return Result.Success(page with
        {
            Items = [.. page.Items.Select(c => c with { BalanceCents = balances.GetValueOrDefault(c.Id) })],
        });
    }
}
