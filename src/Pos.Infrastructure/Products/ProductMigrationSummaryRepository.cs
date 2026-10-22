using Microsoft.EntityFrameworkCore;
using Pos.Application.Products.MigrationSummary;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Products;

public sealed class ProductMigrationSummaryRepository : IProductMigrationSummaryRepository
{
    private readonly PosDbContext _context;

    public ProductMigrationSummaryRepository(PosDbContext context) => _context = context;

    public Task<ProductMigrationSummary?> GetAsync(CancellationToken cancellationToken) =>
        _context.ProductMigrationSummaries.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    public Task MarkShownAsync(CancellationToken cancellationToken) =>
        _context.ProductMigrationSummaries.ExecuteUpdateAsync(s => s.SetProperty(x => x.Shown, true), cancellationToken);
}
