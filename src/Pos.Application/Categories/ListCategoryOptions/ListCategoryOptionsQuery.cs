namespace Pos.Application.Categories.ListCategoryOptions;

/// <summary>Opciones de los selectores: solo activas para asignar; con inactivas para los filtros.</summary>
public sealed record ListCategoryOptionsQuery(bool IncludeInactive);
