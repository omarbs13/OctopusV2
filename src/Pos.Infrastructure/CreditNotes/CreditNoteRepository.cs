using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.CreditNotes;
using Pos.Application.Products;
using Pos.Domain.CreditNotes;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.CreditNotes;

public sealed class CreditNoteRepository : ICreditNoteRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public CreditNoteRepository(PosDbContext context) => _context = context;

    public async Task<long> NextNumberAsync(CancellationToken cancellationToken) =>
        (await _context.CreditNotes.MaxAsync(n => (long?)n.Number, cancellationToken) ?? 0) + 1;

    public Task<CreditNote?> FindByNumberAsync(long number, CancellationToken cancellationToken) =>
        _context.CreditNotes.SingleOrDefaultAsync(n => n.Number == number, cancellationToken);

    public Task<CreditNote?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.CreditNotes.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Add(CreditNote note) => _context.CreditNotes.Add(note);

    public void AddMovement(CreditNoteMovement movement) => _context.CreditNoteMovements.Add(movement);

    public async Task<long> GetBalanceAsync(Guid creditNoteId, CancellationToken cancellationToken) =>
        await _context.CreditNoteMovements.AsNoTracking()
            .Where(m => m.CreditNoteId == creditNoteId)
            .SumAsync(m => m.Type == CreditNoteMovementType.Redeem ? -m.AmountCents : m.AmountCents, cancellationToken);

    public async Task<int> NextMovementSequenceAsync(Guid creditNoteId, CancellationToken cancellationToken) =>
        (await _context.CreditNoteMovements.AsNoTracking()
            .Where(m => m.CreditNoteId == creditNoteId)
            .MaxAsync(m => (int?)m.Sequence, cancellationToken) ?? 0) + 1;

    public async Task<CreditNotePage> SearchAsync(CreditNoteSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var notes = _context.CreditNotes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search.Folio) && CreditNoteFolio.TryParse(search.Folio, out var number))
        {
            notes = notes.Where(n => n.Number == number);
        }

        var rows =
            from n in notes
            join r in _context.SaleReturns.AsNoTracking() on n.SaleReturnId equals r.Id
            join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
            select new
            {
                n.Id,
                n.Number,
                n.CreatedAt,
                n.InitialCents,
                s.FolioNumber,
                Balance = _context.CreditNoteMovements
                    .Where(m => m.CreditNoteId == n.Id)
                    .Sum(m => m.Type == CreditNoteMovementType.Redeem ? -m.AmountCents : m.AmountCents),
            };
        if (search.OnlyWithBalance)
        {
            rows = rows.Where(x => x.Balance > 0);
        }

        var total = await rows.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var items = (await rows
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * search.PageSize)
                .Take(search.PageSize)
                .ToListAsync(cancellationToken))
            .Select(x => new CreditNoteListItemDto(
                x.Id,
                CreditNoteFolio.Format(x.Number),
                x.CreatedAt,
                x.InitialCents,
                x.Balance,
                Folio.Format(x.FolioNumber)))
            .ToList();
        return new CreditNotePage(items, total, page, search.PageSize);
    }

    public async Task<CreditNoteDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var head = await (
                from n in _context.CreditNotes.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on n.SaleReturnId equals r.Id
                join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
                where n.Id == id
                select new { n.Id, n.Number, n.CreatedAt, n.InitialCents, SaleNumber = s.FolioNumber, ReturnNumber = r.Number })
            .SingleOrDefaultAsync(cancellationToken);
        if (head is null)
        {
            return null;
        }

        var movements = await _context.CreditNoteMovements.AsNoTracking()
            .Where(m => m.CreditNoteId == id)
            .OrderBy(m => m.Sequence)
            .Select(m => new
            {
                m.Sequence,
                m.CreatedAt,
                m.Type,
                m.AmountCents,
                m.CreatedBy,
                SaleNumber = _context.Sales.Where(s => s.Id == m.SaleId).Select(s => (long?)s.FolioNumber).FirstOrDefault(),
                ReturnNumber = _context.SaleReturns.Where(r => r.Id == m.SaleReturnId).Select(r => (long?)r.Number).FirstOrDefault(),
                UserName = _context.Users.Where(u => u.Id == m.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = movements.Select(m => new CreditNoteMovementDto(
            m.Sequence,
            m.CreatedAt,
            m.Type,
            m.AmountCents,
            m.SaleNumber is { } sale ? Folio.Format(sale) : null,
            m.ReturnNumber is { } ret ? ReturnFolio.Format(ret) : null,
            m.UserName ?? SystemUser.NameOf(m.CreatedBy)))
            .ToList();
        var balance = items.Sum(m => m.Type == CreditNoteMovementType.Redeem ? -m.AmountCents : m.AmountCents);
        return new CreditNoteDetailDto(
            head.Id,
            CreditNoteFolio.Format(head.Number),
            head.CreatedAt,
            head.InitialCents,
            balance,
            Folio.Format(head.SaleNumber),
            ReturnFolio.Format(head.ReturnNumber),
            items);
    }

    public async Task<CreditNoteTicketData?> GetTicketDataAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from n in _context.CreditNotes.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on n.SaleReturnId equals r.Id
                join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
                where n.Id == id
                select new
                {
                    n.Number,
                    n.CreatedAt,
                    s.FolioNumber,
                    Balance = _context.CreditNoteMovements
                        .Where(m => m.CreditNoteId == n.Id)
                        .Sum(m => m.Type == CreditNoteMovementType.Redeem ? -m.AmountCents : m.AmountCents),
                })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new CreditNoteTicketData(CreditNoteFolio.Format(row.Number), row.Balance, row.CreatedAt, Folio.Format(row.FolioNumber));
    }

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
        })
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
    }
}
