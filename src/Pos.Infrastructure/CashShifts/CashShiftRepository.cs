using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Products;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.CashShifts;

public sealed class CashShiftRepository : ICashShiftRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public CashShiftRepository(PosDbContext context) => _context = context;

    public Task<CashShift?> GetOpenAsync(string registerCode, CancellationToken cancellationToken) =>
        _context.CashShifts
            .Include(s => s.Movements)
            .SingleOrDefaultAsync(s => s.RegisterCode == registerCode && s.Status == CashShiftStatus.Open, cancellationToken);

    public Task<CashShift?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.CashShifts
            .Include(s => s.Movements)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<CashMovementReceiptDto?> FindMovementAsync(Guid movementId, CancellationToken cancellationToken)
    {
        var row = await (
                from m in _context.CashMovements.AsNoTracking()
                join s in _context.CashShifts.AsNoTracking() on m.CashShiftId equals s.Id
                where m.Id == movementId
                select new { m, s.Number, s.OpenedBy })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var names = await NamesAsync([row.m.CreatedBy, row.m.AuthorizedBy ?? row.m.CreatedBy], cancellationToken);
        return new CashMovementReceiptDto(
            row.m.Id,
            ShiftFolio.FormatMovement(row.Number, row.m.Sequence),
            row.m.Type,
            row.m.AmountCents,
            row.m.Reason,
            row.m.CreatedAt,
            NameOf(names, row.m.CreatedBy),
            row.m.CreatedBy,
            row.OpenedBy,
            row.m.AuthorizedBy is { } authorizer ? NameOf(names, authorizer) : null);
    }

    public async Task<ShiftReportDto?> GetReportAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _context.CashShifts.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == shiftId && s.Status == CashShiftStatus.Closed, cancellationToken);
        if (shift?.ClosedBy is not { } closedBy)
        {
            return null;
        }

        var names = await NamesAsync([shift.OpenedBy, closedBy], cancellationToken);
        return new ShiftReportDto(
            shift.Id,
            shift.Folio,
            CashRegister.DisplayName,
            shift.OpenedBy,
            NameOf(names, shift.OpenedBy),
            closedBy,
            NameOf(names, closedBy),
            shift.OpenedAt,
            shift.ClosedAt!.Value,
            shift.OpeningFloatCents,
            shift.SalesCount ?? 0,
            shift.CancelledCount ?? 0,
            shift.CashSalesCents ?? 0,
            shift.CashCancelledCents ?? 0,
            shift.CardCents ?? 0,
            shift.TransferCents ?? 0,
            shift.DepositsCents ?? 0,
            shift.WithdrawalsCents ?? 0,
            shift.ExpectedCashCents ?? 0,
            shift.CountedCashCents ?? 0,
            shift.DifferenceCents ?? 0,
            shift.ClosingComment,
            shift.CashRefundsCents ?? 0,
            shift.NonCashRefundsCents ?? 0,
            shift.CreditNotesIssuedCents ?? 0);
    }

    public async Task<long> NextNumberAsync(CancellationToken cancellationToken) =>
        (await _context.CashShifts.MaxAsync(s => (long?)s.Number, cancellationToken) ?? 0) + 1;

    public void Add(CashShift shift) => _context.CashShifts.Add(shift);

    public void AddMovement(CashMovement movement) => _context.CashMovements.Add(movement);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey,
        } unique)
        {
            _context.ChangeTracker.Clear();
            return DuplicateOf(unique.Message);
        }
    }

    public async Task<ShiftPage> SearchAsync(ShiftSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var shifts = Filtered(search);
        var total = await shifts.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var rows = await shifts
            .OrderByDescending(s => s.OpenedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Number,
                s.OpenedBy,
                s.OpenedAt,
                s.ClosedAt,
                s.Status,
                s.TotalSoldCents,
                s.DifferenceCents,
                OpenedByName = _context.Users.Where(u => u.Id == s.OpenedBy).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // Total vendido de los abiertos: subconsulta agrupada sobre la página (a lo más 100 turnos).
        var openIds = rows.Where(r => r.Status == CashShiftStatus.Open).Select(r => (Guid?)r.Id).ToList();
        var openTotals = openIds.Count == 0
            ? []
            : await _context.Sales.AsNoTracking()
                .Where(s => s.Status == SaleStatus.Completed && openIds.Contains(s.CashShiftId))
                .GroupBy(s => s.CashShiftId)
                .Select(g => new { ShiftId = g.Key, Cents = g.Sum(s => s.TotalCents - s.ReturnedCents) })
                .ToDictionaryAsync(x => x.ShiftId!.Value, x => x.Cents, cancellationToken);

        var items = rows
            .Select(r => new ShiftListItemDto(
                r.Id,
                ShiftFolio.Format(r.Number),
                r.OpenedByName ?? SystemUser.NameOf(r.OpenedBy),
                r.OpenedAt,
                r.ClosedAt,
                r.Status,
                r.Status == CashShiftStatus.Open ? openTotals.GetValueOrDefault(r.Id) : r.TotalSoldCents ?? 0,
                r.Status == CashShiftStatus.Open ? null : r.DifferenceCents))
            .ToList();
        return new ShiftPage(items, total, page, search.PageSize);
    }

    public async Task<ShiftDetailDto?> GetDetailAsync(Guid id, ShiftSalesTotals openShiftTotals, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(openShiftTotals);

        var shift = await _context.CashShifts.AsNoTracking()
            .Include(s => s.Movements)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (shift is null)
        {
            return null;
        }

        var userIds = shift.Movements
            .SelectMany(m => new[] { m.CreatedBy, m.AuthorizedBy ?? m.CreatedBy })
            .Append(shift.OpenedBy)
            .Append(shift.ClosedBy ?? shift.OpenedBy)
            .Distinct()
            .ToList();
        var names = await NamesAsync(userIds, cancellationToken);

        var movements = shift.Movements
            .OrderBy(m => m.Sequence)
            .Select(m => new CashMovementDto(
                m.Id,
                ShiftFolio.FormatMovement(shift.Number, m.Sequence),
                m.Sequence,
                m.CreatedAt,
                m.Type,
                m.AmountCents,
                m.Reason,
                NameOf(names, m.CreatedBy),
                m.AuthorizedBy is { } by ? NameOf(names, by) : null))
            .ToList();

        var reconciliation = shift.Status == CashShiftStatus.Closed
            ? new ShiftReconciliationDto(
                IsSnapshot: true,
                shift.SalesCount ?? 0,
                shift.CancelledCount ?? 0,
                shift.TotalSoldCents ?? 0,
                shift.CashSalesCents ?? 0,
                shift.CashCancelledCents ?? 0,
                shift.CardCents ?? 0,
                shift.TransferCents ?? 0,
                shift.DepositsCents ?? 0,
                shift.WithdrawalsCents ?? 0,
                shift.ExpectedCashCents ?? 0,
                shift.CountedCashCents,
                shift.DifferenceCents,
                shift.ClosingComment,
                shift.CashRefundsCents ?? 0,
                shift.NonCashRefundsCents ?? 0,
                shift.CreditNotesIssuedCents ?? 0)
            : new ShiftReconciliationDto(
                IsSnapshot: false,
                openShiftTotals.SalesCount,
                openShiftTotals.CancelledCount,
                openShiftTotals.TotalSoldCents,
                openShiftTotals.CashSalesCents,
                openShiftTotals.CashCancelledCents,
                openShiftTotals.CardCents,
                openShiftTotals.TransferCents,
                shift.DepositsTotalCents,
                shift.WithdrawalsTotalCents,
                shift.ExpectedCash(openShiftTotals),
                CountedCashCents: null,
                DifferenceCents: null,
                Comment: null,
                openShiftTotals.CashRefundsCents,
                openShiftTotals.NonCashRefundsCents,
                openShiftTotals.CreditNotesIssuedCents);

        return new ShiftDetailDto(
            shift.Id,
            shift.Version,
            shift.Folio,
            shift.Status,
            NameOf(names, shift.OpenedBy),
            shift.OpenedAt,
            shift.OpeningFloatCents,
            shift.ClosedAt,
            shift.ClosedBy is { } closedBy ? NameOf(names, closedBy) : null,
            [],
            movements,
            reconciliation);
    }

    private IQueryable<CashShift> Filtered(ShiftSearch search)
    {
        var shifts = _context.CashShifts.AsNoTracking().AsQueryable();
        if (search.FromUtc is { } from)
        {
            shifts = shifts.Where(s => s.OpenedAt >= from);
        }

        if (search.ToUtcExclusive is { } to)
        {
            shifts = shifts.Where(s => s.OpenedAt < to);
        }

        if (search.UserId is { } user)
        {
            shifts = shifts.Where(s => s.OpenedBy == user);
        }

        if (search.Status is { } status)
        {
            shifts = shifts.Where(s => s.Status == status);
        }

        return shifts;
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await _context.Users.AsNoTracking()
            .Where(u => list.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private static string NameOf(Dictionary<Guid, string> names, Guid userId) =>
        names.GetValueOrDefault(userId) ?? SystemUser.NameOf(userId);

    /// <summary>Distingue qué índice único se violó a partir del mensaje de SQLite.</summary>
    private static SaveOutcome DuplicateOf(string message) => message switch
    {
        _ when message.Contains("CashShifts.RegisterCode", StringComparison.Ordinal) => SaveOutcome.Duplicate(CashShiftFields.OpenPerRegister),
        _ when message.Contains("CashShifts.Number", StringComparison.Ordinal) => SaveOutcome.Duplicate(CashShiftFields.Number),
        _ => SaveOutcome.Conflict,
    };
}
