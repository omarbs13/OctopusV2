namespace Pos.Application.Products.SearchProducts;

/// <summary>Búsqueda paginada de productos.</summary>
/// <param name="Text">Texto buscado; nulo o vacío lista todo.</param>
/// <param name="IncludeInactive">Incluir inactivos; los borrados nunca se incluyen.</param>
/// <param name="Page">Página solicitada, base 1.</param>
/// <param name="LocateProductId">
/// Si se indica y el producto es visible con estos criterios, se devuelve la página que lo contiene
/// en lugar de <paramref name="Page"/> (por ejemplo, tras guardarlo).
/// </param>
public sealed record SearchProductsQuery(string? Text, bool IncludeInactive, int Page = 1, Guid? LocateProductId = null);
