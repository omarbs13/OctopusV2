using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.Audit;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Audit;

/// <summary>
/// Consulta de la bitácora con los nombres de los usuarios (<c>LEFT JOIN Users</c>); solo lectura (007,
/// FR-027). Los filtros siguen los índices "igualdad + fecha" de research §10 (018).
/// </summary>
public sealed class AuditLogReader : IAuditLogReader
{
    private readonly PosDbContext _context;

    public AuditLogReader(PosDbContext context) => _context = context;

    public async Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var entries = Filtered(search.Filter);
        var total = await entries.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        // Se pagina antes de unir los nombres: así el orden sale del índice (CreatedAt, Id) sin ordenar todo
        // lo filtrado, y los cambios (JSON) se leen solo para las filas de la página (research §10).
        var pageEntries = Ordered(entries, search.Filter)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize);
        var rows = await Project(pageEntries).ToListAsync(cancellationToken);

        return new AuditPage([.. InOrder(rows, search.Filter).Select(ToRow)], total, page, search.PageSize);
    }

    public async Task<IReadOnlyList<AuditRow>> ListAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var rows = await Project(Ordered(Filtered(filter), filter)).ToListAsync(cancellationToken);
        return [.. InOrder(rows, filter).Select(ToRow)];
    }

    /// <summary>Entradas que cumplen el filtro; lo comparten la búsqueda paginada y la exportación.</summary>
    private IQueryable<AuditEntry> Filtered(AuditFilter filter)
    {
        var entries = _context.AuditEntries.AsNoTracking();
        if (filter.FromUtc is { } from)
        {
            entries = entries.Where(e => e.CreatedAt >= from);
        }

        if (filter.ToUtcExclusive is { } to)
        {
            entries = entries.Where(e => e.CreatedAt < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action;
            entries = entries.Where(e => e.Action == action);
        }

        if (filter.UserId is { } userId)
        {
            // Usuario involucrado: autor, autorizador o afectado por la entrada.
            entries = entries.Where(e =>
                e.CreatedBy == userId
                || e.AuthorizedBy == userId
                || (e.EntityType == AuditActions.UserEntity && e.EntityId == userId));
        }

        if (filter.Entity is { } group)
        {
            var criteria = AuditEntityGroups.CriteriaFor(group);
            var types = criteria.EntityTypes.ToArray();
            var included = criteria.IncludedActions.ToArray();
            var excluded = criteria.ExcludedActions.ToArray();
            entries = entries.Where(e =>
                included.Contains(e.Action)
                || (types.Contains(e.EntityType) && !excluded.Contains(e.Action)));
        }

        if (filter.Record is { } record)
        {
            var entityType = record.EntityType;
            var entityId = record.EntityId;
            entries = entries.Where(e => e.EntityType == entityType && e.EntityId == entityId);
        }

        return entries;
    }

    /// <summary>Más reciente primero; el historial de un registro, en orden cronológico (Historia 2, escenario 5).</summary>
    private static IQueryable<AuditEntry> Ordered(IQueryable<AuditEntry> entries, AuditFilter filter) =>
        filter.Record is null
            ? entries.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            : entries.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id);

    /// <summary>El orden de la consulta no sobrevive a la unión con usuarios: se vuelve a aplicar sobre las filas leídas.</summary>
    private static IEnumerable<EntryProjection> InOrder(IEnumerable<EntryProjection> rows, AuditFilter filter) =>
        filter.Record is null
            ? rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            : rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);

    private IQueryable<EntryProjection> Project(IQueryable<AuditEntry> entries) =>
        from e in entries
        join author in _context.Users on e.CreatedBy equals author.Id into authors
        from author in authors.DefaultIfEmpty()
        join authorizer in _context.Users on e.AuthorizedBy equals authorizer.Id into authorizers
        from authorizer in authorizers.DefaultIfEmpty()
        select new EntryProjection(
            e.Id,
            e.CreatedAt,
            e.Action,
            e.EntityType,
            e.EntityId,
            e.EntityName,
            e.CreatedBy,
            author == null ? null : author.UserName,
            e.AuthorizedBy,
            authorizer == null ? null : authorizer.UserName,
            e.Reason,
            e.Details,
            e.Changes);

    private static AuditRow ToRow(EntryProjection r) => new(
        r.Id,
        r.CreatedAt,
        r.Action,
        r.EntityType,
        r.EntityId,
        r.EntityName,
        r.AuthorName ?? SystemUser.NameOf(r.CreatedBy),
        r.AuthorizedBy is null ? null : r.AuthorizerName ?? SystemUser.NameOf(r.AuthorizedBy.Value),
        r.Reason,
        r.Details,
        r.Changes is null ? [] : [.. r.Changes]);

    private sealed record EntryProjection(
        Guid Id,
        DateTime CreatedAt,
        string Action,
        string EntityType,
        Guid EntityId,
        string? EntityName,
        Guid CreatedBy,
        string? AuthorName,
        Guid? AuthorizedBy,
        string? AuthorizerName,
        string? Reason,
        string? Details,
        IReadOnlyList<AuditFieldChange>? Changes);
}
