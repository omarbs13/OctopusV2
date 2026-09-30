using Pos.Application.Products;
using Pos.Domain.Business;

namespace Pos.Application.Business;

/// <summary>Persistencia de los datos del negocio (una fila por instalación).</summary>
public interface IBusinessProfileRepository
{
    /// <summary>La fila única, con seguimiento de cambios, o nula si aún no se captura.</summary>
    Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken);

    void Add(BusinessProfile profile);

    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
