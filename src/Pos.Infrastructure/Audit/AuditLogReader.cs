using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Audit;

/// <summary>Consulta de la bitácora con los nombres de los usuarios (<c>LEFT JOIN Users</c>); solo lectura (007, FR-027).</summary>
public sealed class AuditLogReader : IAuditLogReader
{
    private readonly PosDbContext _context;

    public AuditLogReader(PosDbContext context) => _context = context;

    public async Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var entries = _context.AuditEntries.AsNoTracking().AsQueryable();
        if (search.FromUtc is { } from)
        {
            entries = entries.Where(e => e.CreatedAt >= from);
        }

        if (search.ToUtcExclusive is { } to)
        {
            entries = entries.Where(e => e.CreatedAt < to);
        }

        if (!string.IsNullOrWhiteSpace(search.Action))
        {
            var action = search.Action;
            entries = entries.Where(e => e.Action == action);
        }

        if (search.UserId is { } userId)
        {
            // Usuario involucrado: autor, autorizador o afectado por la entrada.
            entries = entries.Where(e =>
                e.CreatedBy == userId
                || e.AuthorizedBy == userId
                || (e.EntityType == AuditActions.UserEntity && e.EntityId == userId));
        }

        var total = await entries.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var rows = await (
                from e in entries
                join author in _context.Users on e.CreatedBy equals author.Id into authors
                from author in authors.DefaultIfEmpty()
                join authorizer in _context.Users on e.AuthorizedBy equals authorizer.Id into authorizers
                from authorizer in authorizers.DefaultIfEmpty()
                orderby e.CreatedAt descending, e.Id descending
                select new
                {
                    e.Id,
                    e.CreatedAt,
                    e.Action,
                    e.CreatedBy,
                    AuthorName = author == null ? null : author.FullName,
                    e.AuthorizedBy,
                    AuthorizerName = authorizer == null ? null : authorizer.FullName,
                    e.EntityType,
                    e.EntityId,
                    e.Details,
                })
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AuditRow(
                r.Id,
                r.CreatedAt,
                r.Action,
                r.AuthorName ?? SystemUser.NameOf(r.CreatedBy),
                r.AuthorizedBy is null ? null : r.AuthorizerName ?? SystemUser.NameOf(r.AuthorizedBy.Value),
                r.EntityType,
                r.EntityId,
                r.Details))
            .ToList();
        return new AuditPage(items, total, page, search.PageSize);
    }
}
