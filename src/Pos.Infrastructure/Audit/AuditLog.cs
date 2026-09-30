using Pos.Application.Abstractions;
using Pos.Domain.Audit;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Audit;

/// <summary>
/// Agrega la entrada al <see cref="PosDbContext"/> del ámbito sin guardar: viaja en la transacción
/// del caso de uso que la generó (research §10).
/// </summary>
public sealed class AuditLog : IAuditLog
{
    private readonly PosDbContext _context;

    public AuditLog(PosDbContext context) => _context = context;

    public void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null) =>
        _context.AuditEntries.Add(AuditEntry.Create(action, entityType, entityId, details, authorizedBy));

    public Task SaveAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
