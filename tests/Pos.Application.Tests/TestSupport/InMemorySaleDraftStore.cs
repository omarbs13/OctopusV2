using Pos.Application.Sales;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Almacén de borradores en memoria, uno por usuario.</summary>
public sealed class InMemorySaleDraftStore : ISaleDraftStore
{
    public Dictionary<Guid, StoredDraft> Drafts { get; } = [];

    public Guid CurrentUserId { get; set; } = Guid.Parse("33333333-3333-7333-8333-333333333333");

    public Task<StoredDraft?> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Drafts.GetValueOrDefault(CurrentUserId));

    public Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken cancellationToken)
    {
        Drafts[CurrentUserId] = new StoredDraft(draftId, lines);
        return Task.CompletedTask;
    }

    public void Remove() => Drafts.Remove(CurrentUserId);

    public Task DiscardAsync(CancellationToken cancellationToken)
    {
        Drafts.Remove(CurrentUserId);
        return Task.CompletedTask;
    }

    public Task<bool> HasForAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Drafts.ContainsKey(userId));

    public Task<bool> RemoveForAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Drafts.Remove(userId));

    public Task ReassignAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        if (Drafts.Remove(fromUserId, out var draft))
        {
            Drafts[toUserId] = draft;
        }

        return Task.CompletedTask;
    }
}
