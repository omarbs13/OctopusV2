using Pos.Application.Products;
using Pos.Domain.Categories;

namespace Pos.Application.Categories;

/// <summary>Persistencia del agregado Categoría (016). Comparte la unidad de trabajo con los demás repositorios.</summary>
public interface ICategoryRepository
{
    /// <summary>Categoría no borrada con seguimiento de cambios, o nula.</summary>
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Indica si otra categoría no borrada (distinta de <paramref name="excludingId"/>) tiene la misma clave.</summary>
    Task<bool> NameExistsAsync(string nameKey, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>Productos no borrados (activos o inactivos) con la categoría.</summary>
    Task<int> CountProductsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Categorías no borradas ordenadas por nombre; <paramref name="nameKey"/> ya normalizado, o nulo.</summary>
    Task<IReadOnlyList<CategoryListItemDto>> SearchAsync(string? nameKey, CategoryStatusFilter status, CancellationToken cancellationToken);

    /// <summary>Opciones no borradas ordenadas por nombre; solo activas salvo <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<CategoryOptionDto>> ListOptionsAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <summary>Quita la categoría a los productos borrados que aún la tienen (research §4); se ejecuta de inmediato.</summary>
    Task ClearFromDeletedProductsAsync(Guid id, CancellationToken cancellationToken);

    void Add(Category category);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Conflict"/> por concurrencia y
    /// <see cref="SaveStatus.Duplicate"/> con <see cref="CategoryFields.Name"/> si el nombre ya existe.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
