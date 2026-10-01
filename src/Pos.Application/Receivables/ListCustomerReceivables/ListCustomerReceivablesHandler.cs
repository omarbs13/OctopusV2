using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Receivables;
using Pos.Domain.Users;

namespace Pos.Application.Receivables.ListCustomerReceivables;

/// <summary>Ventas a crédito del cliente con su saldo, estado y días vencido (pestaña "Ventas a crédito").</summary>
public sealed class ListCustomerReceivablesHandler
{
    private readonly IAccessControl _access;
    private readonly IReceivableRepository _receivables;
    private readonly CreditAging _aging;

    public ListCustomerReceivablesHandler(IAccessControl access, IReceivableRepository receivables, CreditAging aging)
    {
        _access = access;
        _receivables = receivables;
        _aging = aging;
    }

    public async Task<Result<ReceivablePage>> HandleAsync(ListCustomerReceivablesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReceivablePage>(access.Error!);
        }

        var page = await _receivables.ListByCustomerAsync(
            query.CustomerId,
            query.OnlyPending,
            Math.Max(query.Page, 1),
            ReceivablePage.DefaultPageSize,
            cancellationToken);
        var aging = _aging.Now();
        return Result.Success(page with
        {
            Items = [.. page.Items.Select(r => r with
            {
                DaysOverdue = r.Status == ReceivableStatus.Pending ? aging.DaysOverdue(r.SaleDateUtc) : 0,
            })],
        });
    }
}
