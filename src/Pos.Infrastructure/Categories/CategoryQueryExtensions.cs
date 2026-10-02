using Pos.Application.Categories;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Categories;

/// <summary>Traducción única del filtro de categoría a SQL para listados y reportes (016, research §8).</summary>
internal static class CategoryQueryExtensions
{
    public static IQueryable<Product> WhereCategory(this IQueryable<Product> products, CategoryFilter filter) =>
        filter.Kind switch
        {
            CategoryFilterKind.Uncategorized => products.Where(p => p.CategoryId == null),
            CategoryFilterKind.Only => products.Where(p => p.CategoryId == filter.CategoryId),
            _ => products,
        };
}
