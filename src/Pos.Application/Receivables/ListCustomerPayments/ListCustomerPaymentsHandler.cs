using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Receivables.ListCustomerPayments;

/// <summary>Historial de abonos del cliente con los anulados visibles (pestaña "Abonos").</summary>
public sealed class ListCustomerPaymentsHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerPaymentRepository _payments;

    public ListCustomerPaymentsHandler(IAccessControl access, ICustomerPaymentRepository payments)
    {
        _access = access;
        _payments = payments;
    }

    public async Task<Result<CustomerPaymentPage>> HandleAsync(ListCustomerPaymentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CustomerPaymentPage>(access.Error!);
        }

        return Result.Success(await _payments.ListByCustomerAsync(
            query.CustomerId,
            Math.Max(query.Page, 1),
            CustomerPaymentPage.DefaultPageSize,
            cancellationToken));
    }
}
