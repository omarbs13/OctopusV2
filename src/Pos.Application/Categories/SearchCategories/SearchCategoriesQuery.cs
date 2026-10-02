namespace Pos.Application.Categories.SearchCategories;

/// <summary>Busca por nombre (sin acentos ni mayúsculas) y filtra por estado (Historia 1, escenario 9).</summary>
public sealed record SearchCategoriesQuery(string? Text, CategoryStatusFilter Status = CategoryStatusFilter.Active);
