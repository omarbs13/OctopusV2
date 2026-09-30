namespace Pos.Application.Audit;

/// <summary>
/// Criterios de la consulta de la bitácora; el límite superior de fecha es exclusivo (UTC).
/// <c>UserId</c> es el usuario involucrado: autor, autorizador o afectado.
/// </summary>
public sealed record AuditSearch(DateTime? FromUtc, DateTime? ToUtcExclusive, Guid? UserId, string? Action, int Page, int PageSize);

public sealed record AuditRow(
    Guid Id,
    DateTime CreatedAtUtc,
    string Action,
    string UserName,
    string? AuthorizedByName,
    string EntityType,
    Guid EntityId,
    string? Details);

public sealed record AuditPage(IReadOnlyList<AuditRow> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}
