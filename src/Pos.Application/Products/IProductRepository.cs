using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Persistencia del agregado Producto. Específico del agregado; no hay repositorios genéricos.</summary>
public interface IProductRepository
{
    /// <summary>
    /// Producto no borrado, o nulo. Con <paramref name="includeImage"/> carga también su imagen;
    /// sin ella, <see cref="Product.Image"/> queda nula aunque el producto tenga imagen.
    /// </summary>
    Task<Product?> GetAsync(Guid id, bool includeImage, CancellationToken cancellationToken);

    /// <summary>Productos activos y no borrados.</summary>
    Task<long> CountActiveAsync(CancellationToken cancellationToken);

    /// <summary>Indica si otro producto no borrado usa el SKU (ya normalizado).</summary>
    Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>Indica si otro producto no borrado usa el código de barras.</summary>
    Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Indica si un producto no borrado tiene como código de barras o SKU el texto dado, comparado sin
    /// distinguir mayúsculas ni espacios de los extremos (015: el código de un cupón no puede coincidir).
    /// </summary>
    Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Página de productos visibles según los criterios, ordenada por nombre y SKU.</summary>
    Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Página (base 1) que contiene al producto con los criterios dados, o nulo si el producto no
    /// es visible con esos criterios.
    /// </summary>
    Task<int?> LocatePageAsync(ProductSearch search, Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// Productos cuyo código de barras es exactamente <paramref name="code"/> o cuyo SKU normalizado
    /// lo es. Incluye inactivos y borrados, sin seguimiento, para poder informar el motivo (005, §8).
    /// </summary>
    Task<IReadOnlyList<Product>> FindForSaleAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Búsqueda por nombre, SKU o código de barras para vender: activos primero, sin borrados, hasta
    /// <paramref name="limit"/> resultados y sin seguimiento. <paramref name="nameText"/> ya viene normalizado.
    /// </summary>
    Task<IReadOnlyList<Product>> SearchForSaleAsync(string nameText, int limit, CancellationToken cancellationToken);

    /// <summary>Productos por id, sin seguimiento; los borrados solo con <paramref name="includeDeleted"/>.</summary>
    Task<IReadOnlyList<Product>> GetManyAsync(
        IReadOnlyCollection<Guid> ids,
        bool includeDeleted,
        CancellationToken cancellationToken);

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
/// <param name="Page">Página solicitada, base 1; si excede el total se devuelve la última.</param>
/// <param name="PageSize">Registros por página.</param>
public sealed record ProductSearch(
    string? NameText,
    string? SkuText,
    string? BarcodeText,
    bool BarcodeExact,
    bool IncludeInactive,
    int Page,
    int PageSize);

/// <summary>Página del listado de productos (FR-006 y FR-007).</summary>
/// <param name="Items">Productos de la página, ordenados por nombre y SKU.</param>
/// <param name="TotalCount">Productos que cumplen los criterios; nunca incluye borrados.</param>
/// <param name="Page">Página devuelta, base 1, ya ajustada al rango válido.</param>
/// <param name="PageSize">Registros por página.</param>
public sealed record ProductPage(IReadOnlyList<ProductListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    /// <summary>Total de páginas; al menos 1 aunque no haya registros.</summary>
    public int TotalPages => PageCount(TotalCount, PageSize);

    public static int PageCount(long totalCount, int pageSize) =>
        totalCount <= 0 ? 1 : (int)((totalCount + pageSize - 1) / pageSize);
}

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
