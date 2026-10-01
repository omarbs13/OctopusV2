using Pos.Application.Abstractions;
using Pos.Application.Returns;
using Pos.Application.Users.Access;
using Pos.Domain.CreditNotes;
using Pos.Domain.Users;

namespace Pos.Application.CreditNotes.GetCreditNoteBalance;

/// <summary>
/// Saldo de una nota de crédito por folio, para el cobro (<c>Sell</c>). Un folio inexistente o sin saldo
/// responde igual y no revela origen ni usos de la nota (Principio IX).
/// </summary>
public sealed class GetCreditNoteBalanceHandler
{
    private readonly IAccessControl _access;
    private readonly ICreditNoteRepository _notes;

    public GetCreditNoteBalanceHandler(IAccessControl access, ICreditNoteRepository notes)
    {
        _access = access;
        _notes = notes;
    }

    public async Task<Result<CreditNoteBalance>> HandleAsync(string? folio, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CreditNoteBalance>(access.Error!);
        }

        // El módulo Devoluciones debe estar activo para usar notas de crédito.
        var module = await _access.CheckAsync(Permission.ProcessReturns, cancellationToken);
        if (module.Error is Abstractions.ModuleNotLicensed)
        {
            return Result.Failure<CreditNoteBalance>(module.Error);
        }

        if (string.IsNullOrWhiteSpace(folio))
        {
            return Result.Failure<CreditNoteBalance>(new ValidationFailed([new FieldError(ReturnFields.Folio, ReturnMessages.FolioRequired)]));
        }

        if (!CreditNoteFolio.TryParse(folio, out var number))
        {
            return Result.Failure<CreditNoteBalance>(new ValidationFailed([new FieldError(ReturnFields.Folio, ReturnMessages.FolioInvalid)]));
        }

        var note = await _notes.FindByNumberAsync(number, cancellationToken);
        var balance = note is null ? 0 : await _notes.GetBalanceAsync(note.Id, cancellationToken);
        return note is null || balance <= 0
            ? Result.Failure<CreditNoteBalance>(new CreditNoteNotFound())
            : Result.Success(new CreditNoteBalance(note.Id, note.Folio, balance));
    }
}
