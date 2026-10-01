using Pos.Application.Products;
using Pos.Domain.CreditNotes;

namespace Pos.Application.CreditNotes;

/// <summary>
/// Persistencia de notas de crédito. El saldo se calcula con los movimientos (research §5); no hay
/// <c>Update</c> ni <c>Remove</c>. Comparte la unidad de trabajo con los demás repositorios.
/// </summary>
public interface ICreditNoteRepository
{
    /// <summary><c>MAX(Number) + 1</c>; se llama dentro de la transacción de escritura.</summary>
    Task<long> NextNumberAsync(CancellationToken cancellationToken);

    /// <summary>Nota por número de folio, o nula.</summary>
    Task<CreditNote?> FindByNumberAsync(long number, CancellationToken cancellationToken);

    Task<CreditNote?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CreditNote note);

    void AddMovement(CreditNoteMovement movement);

    /// <summary>Saldo = Σ emisión + Σ restauración − Σ uso, de lo guardado en la base.</summary>
    Task<long> GetBalanceAsync(Guid creditNoteId, CancellationToken cancellationToken);

    /// <summary>Siguiente consecutivo de movimiento de la nota.</summary>
    Task<int> NextMovementSequenceAsync(Guid creditNoteId, CancellationToken cancellationToken);

    Task<CreditNotePage> SearchAsync(CreditNoteSearch search, CancellationToken cancellationToken);

    Task<CreditNoteDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Datos para el ticket de la nota; nulo si no existe.</summary>
    Task<CreditNoteTicketData?> GetTicketDataAsync(Guid id, CancellationToken cancellationToken);

    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record CreditNoteTicketData(string Folio, long BalanceCents, DateTime IssuedAtUtc, string SaleFolio);
