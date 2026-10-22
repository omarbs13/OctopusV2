using Pos.Domain.Products;

namespace Pos.Application.Products.MigrationSummary;

/// <summary>Resumen de la migración de productos (026): una sola fila.</summary>
public interface IProductMigrationSummaryRepository
{
    /// <summary>La fila del resumen, o nula si no hubo migración de productos.</summary>
    Task<ProductMigrationSummary?> GetAsync(CancellationToken cancellationToken);

    /// <summary>Marca el resumen como visto, de modo que no vuelve a mostrarse.</summary>
    Task MarkShownAsync(CancellationToken cancellationToken);
}
