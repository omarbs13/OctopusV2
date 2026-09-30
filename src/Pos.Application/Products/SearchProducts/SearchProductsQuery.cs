namespace Pos.Application.Products.SearchProducts;

public sealed record SearchProductsQuery(string? Text, bool IncludeInactive);

public sealed record SearchProductsResult(IReadOnlyList<ProductListItemDto> Items, bool HasMore);
