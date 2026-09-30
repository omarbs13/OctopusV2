using System.Reflection;
using Pos.Application.Products;
using Pos.Domain.Products;

namespace Pos.Application.Tests.TestSupport;

/// <summary>
/// Repositorio de productos en memoria para probar casos de uso y ViewModels. Guarda copias para
/// que un guardado rechazado no altere el estado. Las reglas de unicidad, borrado y versión imitan
/// a la base real; la fidelidad con SQLite se prueba en Pos.Infrastructure.Tests.
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly PropertyInfo VersionProperty = typeof(Product).GetProperty(nameof(Product.Version))!;
    private static readonly PropertyInfo CreatedAtProperty = typeof(Product).GetProperty(nameof(Product.CreatedAt))!;
    private static readonly PropertyInfo UpdatedAtProperty = typeof(Product).GetProperty(nameof(Product.UpdatedAt))!;

    private readonly Dictionary<Guid, Product> _stored = [];
    private readonly List<Product> _pending = [];

    public int SaveCount { get; private set; }

    public int AddCount { get; private set; }

    /// <summary>Si se asigna, cada guardado espera a que se complete (para simular lentitud).</summary>
    public TaskCompletionSource? SaveGate { get; set; }

    /// <summary>Resultado forzado del próximo guardado (por ejemplo, un duplicado por carrera).</summary>
    public SaveOutcome? NextOutcome { get; set; }

    /// <summary>Excepción que lanzará cualquier operación (para simular fallas inesperadas).</summary>
    public Exception? FailWith { get; set; }

    public IReadOnlyCollection<Product> All => _stored.Values;

    /// <summary>Últimos criterios de búsqueda recibidos.</summary>
    public ProductSearch? LastSearch { get; private set; }

    public Product Seed(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        _stored[product.Id] = Clone(product);
        return product;
    }

    /// <summary>Simula que otra operación modificó el producto (incrementa su versión guardada).</summary>
    public void BumpVersion(Guid id)
    {
        var stored = _stored[id];
        VersionProperty.SetValue(stored, stored.Version + 1);
    }

    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.FromResult(_stored.TryGetValue(id, out var p) && !p.IsDeleted ? Clone(p) : null);
    }

    public Task<long> CountActiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.FromResult((long)_stored.Values.Count(p => p.IsActive && !p.IsDeleted));
    }

    public Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.FromResult(_stored.Values.Any(p => !p.IsDeleted && p.Id != excludingId && p.Sku == sku));
    }

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return Task.FromResult(_stored.Values.Any(p => !p.IsDeleted && p.Id != excludingId && p.Barcode == barcode));
    }

    public Task<ProductSearchPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ThrowIfFailing();
        LastSearch = search;
        var matches = _stored.Values
            .Where(p => !p.IsDeleted && (search.IncludeInactive || p.IsActive))
            .Where(p => search.NameText is null
                || p.NameSearch.Contains(search.NameText, StringComparison.Ordinal)
                || p.Sku.Contains(search.SkuText!, StringComparison.Ordinal)
                || (p.Barcode is not null && (search.BarcodeExact
                    ? p.Barcode == search.BarcodeText
                    : p.Barcode.Contains(search.BarcodeText!, StringComparison.Ordinal))))
            .OrderBy(p => p.NameSearch, StringComparer.Ordinal)
            .Select(p => new ProductListItemDto(p.Id, p.Name, p.Sku, p.Barcode, p.Price.Cents, p.IsActive, p.Version))
            .ToList();
        return Task.FromResult(new ProductSearchPage(matches.Take(search.Limit).ToList(), matches.Count > search.Limit));
    }

    public void Add(Product product)
    {
        ThrowIfFailing();
        AddCount++;
        _pending.Add(product);
    }

    public async Task<SaveOutcome> SaveChangesAsync(Product product, int? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        ThrowIfFailing();
        if (SaveGate is not null)
        {
            await SaveGate.Task;
        }

        if (NextOutcome is { } forced)
        {
            NextOutcome = null;
            _pending.Clear();
            return forced;
        }

        var isNew = _pending.Remove(product);
        if (!isNew)
        {
            var stored = _stored[product.Id];
            if (expectedVersion is { } version && stored.Version != version)
            {
                return SaveOutcome.Conflict;
            }

            VersionProperty.SetValue(product, stored.Version + 1);
        }
        else
        {
            CreatedAtProperty.SetValue(product, DateTime.UtcNow);
        }

        UpdatedAtProperty.SetValue(product, DateTime.UtcNow);

        var active = _stored.Values.Where(p => !p.IsDeleted && p.Id != product.Id).ToList();
        if (!product.IsDeleted && active.Any(p => p.Sku == product.Sku))
        {
            return SaveOutcome.Duplicate(ProductFields.Sku);
        }

        if (!product.IsDeleted && product.Barcode is not null && active.Any(p => p.Barcode == product.Barcode))
        {
            return SaveOutcome.Duplicate(ProductFields.Barcode);
        }

        SaveCount++;
        _stored[product.Id] = Clone(product);
        return SaveOutcome.Saved;
    }

    private static Product Clone(Product product) => (Product)CloneMethod.Invoke(product, null)!;

    private void ThrowIfFailing()
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }
    }
}
