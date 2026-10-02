namespace Pos.Application.Categories.CreateCategory;

/// <summary>Alta de categoría (FR-002): nombre obligatorio y descripción opcional; nace activa.</summary>
public sealed record CreateCategoryCommand(string Name, string? Description);
