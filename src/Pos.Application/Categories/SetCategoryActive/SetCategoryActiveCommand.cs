namespace Pos.Application.Categories.SetCategoryActive;

/// <summary>
/// Activa o desactiva una categoría (FR-005). Desactivar una con productos exige <c>Confirmed</c>; sin él
/// devuelve <c>ConfirmationRequired</c> con el número de productos.
/// </summary>
public sealed record SetCategoryActiveCommand(Guid Id, bool IsActive, int ExpectedVersion, bool Confirmed = false);
