namespace Pos.Desktop.Inventory;

/// <summary>Argumento de navegación a Movimientos: muestra solo el historial de un producto (FR-019).</summary>
public sealed record MovementsProductFilter(Guid ProductId, string Name);
