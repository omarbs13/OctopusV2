using Pos.Application.Products;
using Pos.Application.Suppliers;
using Pos.Domain.Suppliers;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Doble mínimo de <see cref="ISupplierRepository"/> para las pruebas de productos (026).</summary>
public sealed class InMemorySupplierRepository : ISupplierRepository
{
    private readonly Dictionary<Guid, Supplier> _suppliers = [];

    public Supplier Seed(Supplier supplier)
    {
        _suppliers[supplier.Id] = supplier;
        return supplier;
    }

    public Task<Supplier?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_suppliers.GetValueOrDefault(id));

    public Task<Supplier?> FindByTaxIdAsync(string taxId, CancellationToken cancellationToken) =>
        Task.FromResult(_suppliers.Values.FirstOrDefault(s => s.TaxId == taxId));

    public void Add(Supplier supplier) => _suppliers[supplier.Id] = supplier;

    public Task<SupplierPage> SearchAsync(SupplierSearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SupplierFilterOption>> ListForFilterAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SupplierOption>> ListActiveAsync(string? searchText, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SupplierOption>>([.. _suppliers.Values
            .Where(s => s.IsActive)
            .Take(limit)
            .Select(s => new SupplierOption(s.Id, s.Name, s.TaxId))]);

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SaveOutcome.Saved);
}
