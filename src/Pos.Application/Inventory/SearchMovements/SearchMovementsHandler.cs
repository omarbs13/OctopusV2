using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Inventory.SearchMovements;

/// <summary>Historial de movimientos, del más reciente al más antiguo (FR-018).</summary>
public sealed class SearchMovementsHandler
{
    private readonly IAccessControl _access;
    private readonly IInventoryRepository _inventory;

    public SearchMovementsHandler(IAccessControl access, IInventoryRepository inventory)
    {
        _access = access;
        _inventory = inventory;
    }

    public async Task<Result<MovementPage>> HandleAsync(SearchMovementsQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewInventory, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<MovementPage>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);

        if (query.FromUtc is { } from && query.ToUtcExclusive is { } to && from > to)
        {
            return Result.Failure<MovementPage>(new ValidationFailed(
                [new FieldError(InventoryFields.DateRange, InventoryMessages.DateRangeInvalid)]));
        }

        var search = new MovementSearch(
            query.ProductId,
            query.Type,
            query.FromUtc,
            query.ToUtcExclusive,
            Math.Max(query.Page, 1),
            MovementPage.DefaultPageSize);
        var page = await _inventory.SearchMovementsAsync(search, cancellationToken);

        // El repositorio resuelve el nombre con Users; si el usuario no aparece, se muestra el id abreviado.
        var items = page.Items
            .Select(m => string.IsNullOrEmpty(m.CreatedByName) ? m with { CreatedByName = SystemUser.NameOf(m.CreatedBy) } : m)
            .ToList();
        return Result.Success(page with { Items = items });
    }
}
