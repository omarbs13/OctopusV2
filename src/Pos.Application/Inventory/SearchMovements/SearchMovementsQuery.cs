using Pos.Domain.Inventory;

namespace Pos.Application.Inventory.SearchMovements;

/// <summary>Historial paginado; las fechas son UTC y el límite superior es exclusivo.</summary>
public sealed record SearchMovementsQuery(
    Guid? ProductId,
    MovementType? Type,
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    int Page = 1);
