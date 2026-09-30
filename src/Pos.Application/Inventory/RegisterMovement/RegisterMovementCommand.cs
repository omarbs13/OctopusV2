using Pos.Domain.Inventory;

namespace Pos.Application.Inventory.RegisterMovement;

/// <summary>Registro de un movimiento; la cantidad llega como texto capturado.</summary>
public sealed record RegisterMovementCommand(
    Guid ProductId,
    MovementType Type,
    string QuantityText,
    string? Reason,
    string? Reference);
