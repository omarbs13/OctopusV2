using Pos.Application.Products;
using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Persistencia del agregado Proveedor (020). Comparte la unidad de trabajo con los demás repositorios.</summary>
public interface ISupplierRepository
{
    /// <summary>Proveedor con seguimiento de cambios, o nulo.</summary>
    Task<Supplier?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Proveedor (activo o inactivo) con el RFC ya normalizado, sin seguimiento, o nulo.</summary>
    Task<Supplier?> FindByTaxIdAsync(string taxId, CancellationToken cancellationToken);

    void Add(Supplier supplier);

    /// <summary>Página de proveedores ordenada por nombre (100 por página).</summary>
    Task<SupplierPage> SearchAsync(SupplierSearch search, CancellationToken cancellationToken);

    /// <summary>Todos los proveedores con su estado, ordenados por nombre (filtro del reporte).</summary>
    Task<IReadOnlyList<SupplierFilterOption>> ListForFilterAsync(CancellationToken cancellationToken);

    /// <summary>Hasta <paramref name="limit"/> proveedores activos cuyo texto de búsqueda contiene <paramref name="searchText"/> (ya normalizado).</summary>
    Task<IReadOnlyList<SupplierOption>> ListActiveAsync(string? searchText, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Conflict"/> por concurrencia y
    /// <see cref="SaveStatus.Duplicate"/> con <see cref="SupplierFields.TaxId"/> si el RFC ya existe.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
