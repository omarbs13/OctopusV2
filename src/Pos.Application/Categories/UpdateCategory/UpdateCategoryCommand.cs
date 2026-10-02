namespace Pos.Application.Categories.UpdateCategory;

/// <summary>Edición de nombre y descripción con concurrencia optimista (FR-004).</summary>
public sealed record UpdateCategoryCommand(Guid Id, string Name, string? Description, int ExpectedVersion);
