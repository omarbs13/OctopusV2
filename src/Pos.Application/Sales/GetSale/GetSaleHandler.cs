using Pos.Application.Abstractions;
using Pos.Application.Returns;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.GetSale;

/// <summary>Detalle de una venta con los valores que se guardaron al venderla; un cajero solo ve las suyas.</summary>
public sealed class GetSaleHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ISaleRepository _sales;
    private readonly IReturnsSettingsStore? _settings;
    private readonly IClock? _clock;

    public GetSaleHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ISaleRepository sales,
        IReturnsSettingsStore? settings = null,
        IClock? clock = null)
    {
        _access = access;
        _currentUser = currentUser;
        _sales = sales;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result<SaleDetailDto>> HandleAsync(GetSaleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var own = await _access.CheckAsync(Permission.ViewOwnSales, cancellationToken);
        if (!own.Allowed)
        {
            return Result.Failure<SaleDetailDto>(own.Error!);
        }

        var detail = await _sales.GetDetailAsync(query.SaleId, cancellationToken);
        if (detail is null)
        {
            return Result.Failure<SaleDetailDto>(new NotFound());
        }

        if (await SaleAccess.CheckOwnershipAsync(_access, _currentUser, detail.CreatedById, cancellationToken) is { } forbidden)
        {
            return Result.Failure<SaleDetailDto>(forbidden);
        }

        // Fuera del plazo de devoluciones la interfaz oculta las acciones (013, FR-006a).
        if (_settings is not null && _clock is not null)
        {
            var windowDays = _settings.Load().ReturnWindowDays;
            detail = detail with { WithinReturnWindow = _clock.UtcNow - detail.CreatedAtUtc <= TimeSpan.FromDays(windowDays) };
        }

        return Result.Success(detail);
    }
}
