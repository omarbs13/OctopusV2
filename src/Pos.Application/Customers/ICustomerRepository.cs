using Pos.Application.Products;
using Pos.Domain.Customers;

namespace Pos.Application.Customers;

/// <summary>
/// Persistencia del agregado Cliente (014). Comparte la unidad de trabajo con los demás repositorios.
/// El saldo no se guarda en el cliente: es la suma de sus cuentas por cobrar pendientes (FR-015).
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Cliente no borrado con seguimiento de cambios, o nulo.</summary>
    Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(Customer customer);

    /// <summary>Página de clientes ordenada por nombre; <c>BalanceCents</c> viene en 0 (el caso de uso lo llena por lote).</summary>
    Task<CustomerPage> SearchAsync(CustomerSearch search, CancellationToken cancellationToken);

    /// <summary>Clientes activos con crédito cuyo texto de búsqueda contiene <paramref name="searchText"/> (ya normalizado), sin seguimiento.</summary>
    Task<IReadOnlyList<Customer>> FindForSaleAsync(string searchText, int limit, CancellationToken cancellationToken);

    /// <summary>Saldo pendiente: Σ <c>BalanceCents</c> de las cuentas <c>PENDING</c> del cliente, en una sola consulta.</summary>
    Task<long> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Saldos de varios clientes en una sola consulta; los que no deben nada vienen en 0.</summary>
    Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Conflict"/> por concurrencia y
    /// <see cref="SaveStatus.Duplicate"/> con <see cref="CustomerFields.TaxId"/> si el RUC ya existe.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
