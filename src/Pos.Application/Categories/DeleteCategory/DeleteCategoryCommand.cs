namespace Pos.Application.Categories.DeleteCategory;

/// <summary>Elimina (borrado lógico) una categoría sin productos no borrados (FR-007).</summary>
public sealed record DeleteCategoryCommand(Guid Id, int ExpectedVersion);
