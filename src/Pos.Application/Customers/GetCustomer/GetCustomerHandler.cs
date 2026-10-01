using Pos.Application.Abstractions;
using Pos.Application.Receivables;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Customers.GetCustomer;

/// <summary>
/// Ficha del cliente con saldo, disponible y días vencido de su cuenta pendiente más antigua; la
/// interfaz no calcula nada (Principio III).
/// </summary>
public sealed class GetCustomerHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IReceivableRepository _receivables;
    private readonly CreditAging _aging;

    public GetCustomerHandler(IAccessControl access, ICustomerRepository customers, IReceivableRepository receivables, CreditAging aging)
    {
        _access = access;
        _customers = customers;
        _receivables = receivables;
        _aging = aging;
    }

    public async Task<Result<CustomerDetailDto>> HandleAsync(GetCustomerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CustomerDetailDto>(access.Error!);
        }

        var customer = await _customers.GetAsync(query.Id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerDetailDto>(new NotFound());
        }

        var balance = await _customers.GetBalanceAsync(customer.Id, cancellationToken);
        var oldest = await _receivables.GetOldestPendingDateAsync(customer.Id, cancellationToken);
        var daysOverdue = oldest is { } saleUtc ? _aging.Now().DaysOverdue(saleUtc) : 0;
        return Result.Success(new CustomerDetailDto(
            customer.Id,
            customer.Version,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.TaxId,
            customer.CreditMode,
            customer.CreditLimitCents,
            customer.IsActive,
            balance,
            Math.Max(0, customer.CreditLimitCents - balance),
            daysOverdue));
    }
}
