using Pos.Application.Abstractions;

namespace Pos.Application.Inventory.SearchMovements;

/// <summary>Historial de movimientos, del más reciente al más antiguo (FR-018).</summary>
public sealed class SearchMovementsHandler
{
    private readonly IInventoryRepository _inventory;

    public SearchMovementsHandler(IInventoryRepository inventory) => _inventory = inventory;

    public async Task<Result<MovementPage>> HandleAsync(SearchMovementsQuery query, CancellationToken cancellationToken)
    {
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

        // El nombre del usuario lo resuelve Application hasta que exista el módulo de usuarios.
        var items = page.Items.Select(m => m with { CreatedByName = SystemUser.NameOf(m.CreatedBy) }).ToList();
        return Result.Success(page with { Items = items });
    }
}
