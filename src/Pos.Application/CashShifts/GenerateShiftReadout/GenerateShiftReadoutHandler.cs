using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.GenerateShiftReadout;

/// <summary>
/// Corte X (017, research §5): guarda en una transacción la lectura del turno abierto con su folio X
/// consecutivo, sin modificar el turno (FR-003). El Cajero necesita la autorización de un
/// administrador (FR-007).
/// </summary>
public sealed partial class GenerateShiftReadoutHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ILogger<GenerateShiftReadoutHandler> _logger;

    public GenerateShiftReadoutHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ICashShiftRepository shifts,
        ISaleRepository sales,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ILogger<GenerateShiftReadoutHandler> logger)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
        _sales = sales;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<GeneratedShiftCut>> HandleAsync(GenerateShiftReadoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.GenerateShiftReadout, command.AuthorizationGrantId, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<GeneratedShiftCut>(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        // Se lee con seguimiento pero no se modifica: SaveChanges no emite UPDATE sobre el turno.
        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null)
        {
            LogNoShift(_currentUser.UserId);
            return Result.Failure<GeneratedShiftCut>(new ShiftRequired());
        }

        var totals = await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken);
        var number = await _shifts.NextCutNumberAsync(ShiftCutType.Readout, cancellationToken);
        var cut = ShiftCut.Readout(number, shift, totals, _currentUser.UserId, access.AuthorizedBy, _clock.UtcNow);
        _shifts.AddCut(cut);

        _audit.Add(
            AuditActions.ShiftReadoutGenerated,
            AuditActions.ShiftCutEntity,
            cut.Id,
            $"Corte X {cut.Folio}. Turno {cut.ShiftFolio}. Total vendido {TicketBuilder.FormatMoney(cut.TotalSoldCents)}",
            access.AuthorizedBy);

        var outcome = await _shifts.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<GeneratedShiftCut>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogGenerated(cut.Id, cut.Folio, shift.Id, _currentUser.UserId, access.AuthorizedBy);
        return Result.Success(new GeneratedShiftCut(cut.Id, cut.Folio, cut.ShiftFolio));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Corte X generado. CutId={CutId} Folio={Folio} ShiftId={ShiftId} UserId={UserId} AuthorizedBy={AuthorizedBy}")]
    private partial void LogGenerated(Guid cutId, string folio, Guid shiftId, Guid userId, Guid? authorizedBy);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Corte X rechazado: no hay turno abierto. UserId={UserId}")]
    private partial void LogNoShift(Guid userId);
}
