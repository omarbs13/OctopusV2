using Pos.Application.Abstractions;

namespace Pos.Application.Products.MigrationSummary;

public sealed class MarkMigrationSummaryShownHandler
{
    private readonly IProductMigrationSummaryRepository _summaries;

    public MarkMigrationSummaryShownHandler(IProductMigrationSummaryRepository summaries) => _summaries = summaries;

    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        await _summaries.MarkShownAsync(cancellationToken);
        return Result.Success();
    }
}
