using Pos.Application.Abstractions;
using Pos.Application.Returns;
using Pos.Application.Users.Access;
using Pos.Domain.CreditNotes;
using Pos.Domain.Users;

namespace Pos.Application.CreditNotes.SearchCreditNotes;

/// <summary>Lista de notas de crédito con saldo calculado, de 100 en 100; solo <c>ManageCreditNotes</c>.</summary>
public sealed class SearchCreditNotesHandler
{
    private readonly IAccessControl _access;
    private readonly ICreditNoteRepository _notes;

    public SearchCreditNotesHandler(IAccessControl access, ICreditNoteRepository notes)
    {
        _access = access;
        _notes = notes;
    }

    public async Task<Result<CreditNotePage>> HandleAsync(SearchCreditNotesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CreditNotePage>(access.Error!);
        }

        var folio = string.IsNullOrWhiteSpace(query.Folio) ? null : query.Folio.Trim();
        if (folio is not null && !CreditNoteFolio.TryParse(folio, out _))
        {
            return Result.Failure<CreditNotePage>(new ValidationFailed([new FieldError(ReturnFields.Folio, ReturnMessages.FolioInvalid)]));
        }

        return Result.Success(await _notes.SearchAsync(
            new CreditNoteSearch(folio, query.OnlyWithBalance, Math.Max(1, query.Page), CreditNotePage.DefaultPageSize),
            cancellationToken));
    }
}
