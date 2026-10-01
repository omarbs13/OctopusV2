namespace Pos.Application.CreditNotes.SearchCreditNotes;

public sealed record SearchCreditNotesQuery(string? Folio, bool OnlyWithBalance, int Page);
