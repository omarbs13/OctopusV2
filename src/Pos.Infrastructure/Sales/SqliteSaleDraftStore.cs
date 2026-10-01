using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Sales;

/// <summary>
/// Borrador durable de la venta en curso: una fila por usuario en SQLite (005 research §7; 007
/// Historia 8). Opera sobre el usuario conectado. Al guardarlo toma el candado de escritura
/// (<c>BEGIN IMMEDIATE</c>), así que espera a una confirmación en curso en lugar de fallar con
/// <c>SQLITE_BUSY</c>, y no revive el borrador de una venta ya registrada.
/// </summary>
public sealed class SqliteSaleDraftStore : ISaleDraftStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PosDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public SqliteSaleDraftStore(PosDbContext context, IClock clock, ICurrentUser currentUser)
    {
        _context = context;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<StoredDraft?> LoadAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var draft = await _context.SaleDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        try
        {
            // 015: un borrador con descuento de venta es un objeto { lines, order }; los demás (y los
            // anteriores a 0.10.0) son la lista de líneas, que admite descuentos de línea opcionales.
            if (draft.LinesJson.TrimStart().StartsWith('{'))
            {
                var stored = JsonSerializer.Deserialize<DraftJson>(draft.LinesJson, Json);
                return new StoredDraft(draft.DraftId, stored?.Lines ?? [], stored?.Order);
            }

            var lines = JsonSerializer.Deserialize<List<DraftLineDto>>(draft.LinesJson, Json) ?? [];
            return new StoredDraft(draft.DraftId, lines);
        }
        catch (JsonException)
        {
            // Un borrador ilegible no debe impedir vender: se trata como si no hubiera.
            return null;
        }
    }

    public async Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, DraftOrderDiscountDto? orderDiscount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var userId = _currentUser.UserId;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await _context.Sales.AnyAsync(s => s.DraftId == draftId, cancellationToken))
        {
            return;
        }

        var json = orderDiscount is null
            ? JsonSerializer.Serialize(lines, Json)
            : JsonSerializer.Serialize(new DraftJson(lines, orderDiscount), Json);
        var existing = await _context.SaleDrafts.SingleOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        if (existing is null)
        {
            _context.SaleDrafts.Add(SaleDraft.Create(userId, draftId, json, _clock.UtcNow));
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
        var draft = _context.SaleDrafts.Find(_currentUser.UserId);
        if (draft is not null)
        {
            _context.SaleDrafts.Remove(draft);
        }
    }

    public async Task DiscardAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        await _context.SaleDrafts.Where(d => d.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> HasForAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.SaleDrafts.AsNoTracking().AnyAsync(d => d.UserId == userId, cancellationToken);

    public async Task<bool> RemoveForAsync(Guid userId, CancellationToken cancellationToken)
    {
        var draft = await _context.SaleDrafts.SingleOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        _context.SaleDrafts.Remove(draft);
        return true;
    }

    public async Task ReassignAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken)
    {
        var source = await _context.SaleDrafts.SingleOrDefaultAsync(d => d.UserId == fromUserId, cancellationToken);
        if (source is null || await _context.SaleDrafts.AnyAsync(d => d.UserId == toUserId, cancellationToken))
        {
            return;
        }

        _context.SaleDrafts.Remove(source);
        _context.SaleDrafts.Add(SaleDraft.Create(toUserId, source.DraftId, source.LinesJson, source.UpdatedAt));
    }

    /// <summary>Formato del borrador con descuento de venta (015, data-model "Borrador").</summary>
    private sealed record DraftJson(IReadOnlyList<DraftLineDto> Lines, DraftOrderDiscountDto? Order);
}
