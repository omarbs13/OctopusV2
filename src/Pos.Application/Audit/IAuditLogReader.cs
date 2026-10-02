namespace Pos.Application.Audit;

/// <summary>Lectura de la bitácora de auditoría (007, FR-027; 018).</summary>
public interface IAuditLogReader
{
    /// <summary>Página de entradas: la más reciente primero; con <c>Record</c>, la más antigua primero.</summary>
    Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken cancellationToken);

    /// <summary>Todas las entradas que cumplen el filtro, en el mismo orden, para exportar (018, FR-023).</summary>
    Task<IReadOnlyList<AuditRow>> ListAsync(AuditFilter filter, CancellationToken cancellationToken);
}
