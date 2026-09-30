using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Persistencia del agregado Producto. Específico del agregado; no hay repositorios genéricos.</summary>
public interface IProductRepository
{
    /// <summary>Producto no borrado, o nulo.</summary>
    Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Productos activos y no borrados.</summary>
    Task<long> CountActiveAsync(CancellationToken cancellationToken);

    /// <summary>Indica si otro producto no borrado usa el SKU (ya normalizado).</summary>
    Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>Indica si otro producto no borrado usa el código de barras.</summary>
    Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken cancellationToken);

    Task<ProductSearchPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken);

    void Add(Product product);

    /// <summary>
    /// Guarda en una sola transacción. Si se indica <paramref name="expectedVersion"/>, el guardado
    /// solo procede si el producto sigue en esa versión.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(
        Product product,
        int? expectedVersion,
        CancellationToken cancellationToken);
}

/// <summary>Criterios de búsqueda ya normalizados por el caso de uso.</summary>
/// <param name="NameText">Texto sin acentos y en minúsculas para buscar en el nombre; nulo si no hay texto.</param>
/// <param name="SkuText">Texto en mayúsculas para buscar en el SKU.</param>
/// <param name="BarcodeText">Texto para buscar en el código de barras.</param>
/// <param name="BarcodeExact">Verdadero si el texto es un código de barras completo (8 a 14 dígitos).</param>
/// <param name="IncludeInactive">Incluir productos inactivos; los borrados nunca se incluyen.</param>
/// <param name="Limit">Máximo de filas a devolver.</param>
public sealed record ProductSearch(
    string? NameText,
    string? SkuText,
    string? BarcodeText,
    bool BarcodeExact,
    bool IncludeInactive,
    int Limit);

public sealed record ProductSearchPage(IReadOnlyList<ProductListItemDto> Items, bool HasMore);

public enum SaveStatus
{
    Saved,
    Conflict,
    Duplicate,
}

public sealed record SaveOutcome(SaveStatus Status, string? DuplicateField = null)
{
    public static SaveOutcome Saved { get; } = new(SaveStatus.Saved);

    public static SaveOutcome Conflict { get; } = new(SaveStatus.Conflict);

    public static SaveOutcome Duplicate(string field) => new(SaveStatus.Duplicate, field);
}
