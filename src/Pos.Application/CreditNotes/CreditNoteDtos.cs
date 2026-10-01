namespace Pos.Application.CreditNotes;

/// <summary>Saldo de una nota de crédito para el cobro; no muestra origen ni usos (Principio IX).</summary>
public sealed record CreditNoteBalance(Guid CreditNoteId, string Folio, long BalanceCents);

public sealed record CreditNoteSearch(string? Folio, bool OnlyWithBalance, int Page, int PageSize);

public sealed record CreditNoteListItemDto(
    Guid Id,
    string Folio,
    DateTime IssuedAtUtc,
    long InitialCents,
    long BalanceCents,
    string SaleFolio);

public sealed record CreditNotePage(IReadOnlyList<CreditNoteListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

public sealed record CreditNoteMovementDto(
    int Sequence,
    DateTime CreatedAtUtc,
    Domain.CreditNotes.CreditNoteMovementType Type,
    long AmountCents,
    string? SaleFolio,
    string? ReturnFolio,
    string CreatedByName);

public sealed record CreditNoteDetailDto(
    Guid Id,
    string Folio,
    DateTime IssuedAtUtc,
    long InitialCents,
    long BalanceCents,
    string SaleFolio,
    string ReturnFolio,
    IReadOnlyList<CreditNoteMovementDto> Movements);
