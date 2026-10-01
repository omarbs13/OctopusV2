using Pos.Application.Abstractions;
using Pos.Application.Receivables;
using Pos.Application.Users.Access;
using Pos.Domain.Customers;
using Pos.Domain.Receivables;
using Pos.Domain.Users;

namespace Pos.Application.Customers.GetCustomerCreditStatus;

/// <summary>
/// Saldo, límite, disponible, excedente y aviso de vencido de un cliente para la venta en curso. Solo
/// lee: la verificación que cuenta la repite <c>ConfirmSale</c> dentro de su transacción (research §4).
/// </summary>
public sealed class GetCustomerCreditStatusHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IReceivableRepository _receivables;
    private readonly CreditAging _aging;

    public GetCustomerCreditStatusHandler(
        IAccessControl access,
        ICustomerRepository customers,
        IReceivableRepository receivables,
        CreditAging aging)
    {
        _access = access;
        _customers = customers;
        _receivables = receivables;
        _aging = aging;
    }

    public async Task<Result<CustomerCreditStatusDto>> HandleAsync(GetCustomerCreditStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.SellOnCredit, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CustomerCreditStatusDto>(access.Error!);
        }

        var customer = await _customers.GetAsync(query.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerCreditStatusDto>(new NotFound());
        }

        if (!customer.CanBuyOnCredit)
        {
            return Result.Failure<CustomerCreditStatusDto>(new CustomerNotEligibleForCredit());
        }

        var balance = await _customers.GetBalanceAsync(customer.Id, cancellationToken);
        var check = CreditPolicy.Check(balance, customer.CreditLimitCents, Math.Max(0, query.SaleTotalCents));
        var oldest = await _receivables.GetOldestPendingDateAsync(customer.Id, cancellationToken);
        var hasOverdue = oldest is { } saleUtc && ReceivableAging.IsOverdue(_aging.Now().DaysOverdue(saleUtc), balance);
        return Result.Success(new CustomerCreditStatusDto(
            balance,
            customer.CreditLimitCents,
            Math.Max(0, customer.CreditLimitCents - balance),
            check.ExcessCents,
            hasOverdue));
    }
}
