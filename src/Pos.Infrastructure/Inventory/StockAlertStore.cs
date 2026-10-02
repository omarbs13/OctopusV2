using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Inventory;

/// <summary>Lecturas y registros de la revisión de alertas de existencia sobre SQLite (022).</summary>
public sealed class StockAlertStore : IStockAlertStore
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public StockAlertStore(PosDbContext context) => _context = context;

    public async Task<IReadOnlyList<StockAlertCandidate>> GetCandidatesAsync(CancellationToken cancellationToken) =>
        await (from p in _context.Products.AsNoTracking()
               where p.DeletedAt == null && p.TracksInventory && p.IsActive
                   && (p.MinimumStockThousandths != null || p.ReorderPointThousandths != null)
               join s in _context.ProductStocks on p.Id equals s.ProductId into stocks
               from s in stocks.DefaultIfEmpty()
               select new StockAlertCandidate(
                   p.Id,
                   s == null ? 0L : s.OnHandThousandths,
                   p.MinimumStockThousandths,
                   p.ReorderPointThousandths))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(Guid ProductId, StockAlertLevel Level)>> GetAcknowledgedAsync(
        Guid userId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var rows = await _context.StockAlertAcknowledgements.AsNoTracking()
            .Where(a => a.UserId == userId && a.LocalDate == localDate)
            .Select(a => new { a.ProductId, a.Level })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => (r.ProductId, r.Level))];
    }

    public void Add(StockAlertAcknowledgement acknowledgement) => _context.StockAlertAcknowledgements.Add(acknowledgement);

    public Task PurgeBeforeAsync(DateOnly localDate, CancellationToken cancellationToken) =>
        _context.StockAlertAcknowledgements.Where(a => a.LocalDate < localDate).ExecuteDeleteAsync(cancellationToken);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey,
        })
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
    }
}
