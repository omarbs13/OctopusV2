using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Settings.GetDiscountSettings;

/// <summary>
/// Lee el límite de descuento; requiere <c>ApplyDiscounts</c> porque el punto de venta lo necesita para
/// saber cuándo pedir autorización antes de aplicar (contracts/application-ports.md).
/// </summary>
public sealed class GetDiscountSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IDiscountSettingsStore _store;

    public GetDiscountSettingsHandler(IAccessControl access, IDiscountSettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result<DiscountSettings>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ApplyDiscounts, cancellationToken);
        return access.Allowed ? Result.Success(_store.Load()) : Result.Failure<DiscountSettings>(access.Error!);
    }
}
