using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Receivables.GetReceivablesSettings;

/// <summary>Lee el plazo de pago de las ventas a crédito; solo <c>ManageCustomerCredit</c>.</summary>
public sealed class GetReceivablesSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReceivablesSettingsStore _store;

    public GetReceivablesSettingsHandler(IAccessControl access, IReceivablesSettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result<ReceivablesSettings>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageCustomerCredit, cancellationToken);
        return access.Allowed ? Result.Success(_store.Load()) : Result.Failure<ReceivablesSettings>(access.Error!);
    }
}
