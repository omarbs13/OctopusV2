namespace Pos.Application.Categories;

public enum CategoryStatusFilter
{
    Active,
    Inactive,
    All,
}

/// <summary>Fila del catálogo; <c>ProductCount</c> cuenta productos no borrados, activos o inactivos.</summary>
public sealed record CategoryListItemDto(Guid Id, string Name, string? Description, bool IsActive, int ProductCount, int Version);

public sealed record CategoryDto(Guid Id, string Name, string? Description, bool IsActive, int ProductCount, int Version);

/// <summary>Opción de los selectores y filtros de categoría.</summary>
public sealed record CategoryOptionDto(Guid Id, string Name, bool IsActive);
