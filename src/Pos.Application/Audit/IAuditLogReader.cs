namespace Pos.Application.Audit;

/// <summary>Lectura de la bitácora de auditoría (007, FR-027).</summary>
public interface IAuditLogReader
{
    Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken cancellationToken);
}
