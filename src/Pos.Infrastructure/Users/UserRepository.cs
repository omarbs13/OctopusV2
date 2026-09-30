using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.Users;
using Pos.Domain.Common;
using Pos.Domain.Users;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Users;

public sealed class UserRepository : IUserRepository
{
    // SQLITE_CONSTRAINT_UNIQUE: https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public UserRepository(PosDbContext context) => _context = context;

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        _context.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task<bool> AnyRealUserAsync(CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => !u.IsSystem, cancellationToken);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive && !u.IsSystem, cancellationToken);

    public async Task<UserPage> SearchAsync(UserSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = _context.Users.AsNoTracking().Where(u => !u.IsSystem);
        if (!search.IncludeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        // Decenas de usuarios por instalación: la búsqueda sin acentos ni mayúsculas se hace en memoria.
        var users = await query.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search.NameText))
        {
            var text = TextNormalizer.ForSearch(search.NameText);
            users = [.. users.Where(u =>
                TextNormalizer.ForSearch(u.FullName).Contains(text, StringComparison.Ordinal)
                || TextNormalizer.ForSearch(u.UserName).Contains(text, StringComparison.Ordinal))];
        }

        var ordered = users
            .OrderBy(u => TextNormalizer.ForSearch(u.FullName), StringComparer.Ordinal)
            .ThenBy(u => u.NormalizedUserName, StringComparer.Ordinal)
            .ToList();

        var total = ordered.Count;
        var page = Math.Clamp(search.Page, 1, total <= 0 ? 1 : (int)((total + search.PageSize - 1L) / search.PageSize));
        var items = ordered
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(u => new UserListItem(u.Id, u.FullName, u.UserName, u.Role, u.IsActive, u.Version))
            .ToList();
        return new UserPage(items, total, page, search.PageSize);
    }

    public async Task<IReadOnlyList<UserOption>> ListCashiersAsync(CancellationToken cancellationToken)
    {
        var users = await _context.Users.AsNoTracking()
            .Where(u => !u.IsSystem && (u.IsActive || _context.Sales.Any(s => s.CreatedBy == u.Id)))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);
        return [.. users
            .OrderBy(u => TextNormalizer.ForSearch(u.FullName), StringComparer.Ordinal)
            .Select(u => new UserOption(u.Id, u.FullName))];
    }

    public void Add(User user) => _context.Users.Add(user);

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
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Duplicate(UserFields.UserName);
        }
    }

    public async Task RecordLoginAttemptAsync(
        Guid id,
        int failedCount,
        DateTime? lockoutEndsAt,
        DateTime? lastLoginAt,
        CancellationToken cancellationToken)
    {
        // Sin pasar por el interceptor: un intento fallido no cambia Version ni UpdatedBy (research §9).
        await _context.Users.Where(u => u.Id == id).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(u => u.FailedLoginCount, failedCount)
                .SetProperty(u => u.LockoutEndsAt, lockoutEndsAt)
                .SetProperty(u => u.LastLoginAt, u => lastLoginAt ?? u.LastLoginAt),
            cancellationToken);

        // Si el usuario está en seguimiento, se alinea para no pisar el contador al guardar después.
        var tracked = _context.ChangeTracker.Entries<User>().FirstOrDefault(e => e.Entity.Id == id);
        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken);
        }
    }
}
