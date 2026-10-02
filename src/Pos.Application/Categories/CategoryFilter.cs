namespace Pos.Application.Categories;

public enum CategoryFilterKind
{
    /// <summary>Sin filtro: todas las categorías y los productos sin categoría.</summary>
    All,

    /// <summary>Solo los productos sin categoría.</summary>
    Uncategorized,

    /// <summary>Solo los productos de <see cref="CategoryFilter.CategoryId"/>.</summary>
    Only,
}

/// <summary>
/// Filtro por categoría del listado de productos y de los reportes (016, research §8). "Sin categoría" no
/// es una categoría registrada, por eso no basta un <c>Guid?</c>. <c>default</c> equivale a <see cref="All"/>.
/// </summary>
public readonly record struct CategoryFilter
{
    private CategoryFilter(CategoryFilterKind kind, Guid? categoryId)
    {
        Kind = kind;
        CategoryId = categoryId;
    }

    public CategoryFilterKind Kind { get; }

    /// <summary>Solo con <see cref="CategoryFilterKind.Only"/>.</summary>
    public Guid? CategoryId { get; }

    public static CategoryFilter All => default;

    public static CategoryFilter Uncategorized => new(CategoryFilterKind.Uncategorized, null);

    public bool IsAll => Kind == CategoryFilterKind.All;

    public static CategoryFilter Only(Guid categoryId) => new(CategoryFilterKind.Only, categoryId);
}
