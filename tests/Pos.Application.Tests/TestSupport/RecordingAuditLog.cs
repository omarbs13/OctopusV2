using Pos.Application.Abstractions;
using Pos.Application.Audit;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Bitácora que guarda lo agregado para poder inspeccionarlo.</summary>
public sealed class RecordingAuditLog : IAuditLog
{
    public List<AuditRecord> Entries { get; } = [];

    public int Saves { get; private set; }

    public void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null) =>
        Entries.Add(new AuditRecord(action, entityType, entityId, Details: details, AuthorizedBy: authorizedBy));

    public void Add(AuditRecord record) => Entries.Add(record);

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
