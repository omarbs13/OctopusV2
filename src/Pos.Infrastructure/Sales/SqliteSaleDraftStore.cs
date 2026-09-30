using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Sales;

/// <summary>
/// Borrador durable de la venta en curso: una sola fila en SQLite (research §7). Al guardarlo toma el
/// candado de escritura (<c>BEGIN IMMEDIATE</c>), así que espera a una confirmación en curso en lugar
/// de fallar con <c>SQLITE_BUSY</c>, y no revive el borrador de una venta ya registrada.
/// </summary>
public sealed class SqliteSaleDraftStore : ISaleDraftStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PosDbContext _context;
    private readonly IClock _clock;

    public SqliteSaleDraftStore(PosDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<StoredDraft?> LoadAsync(CancellationToken cancellationToken)
    {
        var draft = await _context.SaleDrafts.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (draft is null)
        {
            return null;
        }

        try
        {
            var lines = JsonSerializer.Deserialize<List<DraftLineDto>>(draft.LinesJson, Json) ?? [];
            return new StoredDraft(draft.DraftId, lines);
        }
        catch (JsonException)
        {
            // Un borrador ilegible no debe impedir vender: se trata como si no hubiera.
            return null;
        }
    }

    public async Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await _context.Sales.AnyAsync(s => s.DraftId == draftId, cancellationToken))
        {
            return;
        }

        var json = JsonSerializer.Serialize(lines, Json);
        var existing = await _context.SaleDrafts.SingleOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            _context.SaleDrafts.Add(SaleDraft.Create(draftId, json, _clock.UtcNow));
        }
        else
        {
            existing.Replace(draftId, json, _clock.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public void Remove()
    {
        var draft = _context.SaleDrafts.Find(SaleDraft.SingleSlot);
        if (draft is not null)
        {
            _context.SaleDrafts.Remove(draft);
        }
    }

    public async Task DiscardAsync(CancellationToken cancellationToken) =>
        await _context.SaleDrafts.ExecuteDeleteAsync(cancellationToken);
}
