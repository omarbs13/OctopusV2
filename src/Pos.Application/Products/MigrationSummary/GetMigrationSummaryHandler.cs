using Pos.Application.Abstractions;

namespace Pos.Application.Products.MigrationSummary;

/// <summary>
/// Resumen de la migración de productos que aún no se ha mostrado, o nulo (026). Sin permiso especial: lo ve
/// quien inicia sesión primero tras actualizar, cualquiera que sea su rol.
/// </summary>
public sealed class GetMigrationSummaryHandler
{
    private readonly IProductMigrationSummaryRepository _summaries;

    public GetMigrationSummaryHandler(IProductMigrationSummaryRepository summaries) => _summaries = summaries;

    public async Task<Result<MigrationSummaryDto?>> HandleAsync(CancellationToken cancellationToken)
    {
        var summary = await _summaries.GetAsync(cancellationToken);
        return Result.Success<MigrationSummaryDto?>(
            summary is { Shown: false }
                ? new MigrationSummaryDto(summary.MigratedCount, summary.WithoutModelCount, summary.WithoutPackagingCount, summary.DuplicateCount)
                : null);
    }
}
