using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.CreditNotes.GetCreditNoteDetail;

/// <summary>Cabecera y movimientos de una nota de crédito; solo <c>ManageCreditNotes</c>.</summary>
public sealed class GetCreditNoteDetailHandler
{
    private readonly IAccessControl _access;
    private readonly ICreditNoteRepository _notes;

    public GetCreditNoteDetailHandler(IAccessControl access, ICreditNoteRepository notes)
    {
        _access = access;
        _notes = notes;
    }

    public async Task<Result<CreditNoteDetailDto>> HandleAsync(Guid creditNoteId, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageCreditNotes, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CreditNoteDetailDto>(access.Error!);
        }

        var detail = await _notes.GetDetailAsync(creditNoteId, cancellationToken);
        return detail is null ? Result.Failure<CreditNoteDetailDto>(new NotFound()) : Result.Success(detail);
    }
}
