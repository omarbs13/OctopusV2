using Pos.Application.Products;
using Pos.Application.Users;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Tests.TestSupport;

/// <summary>
/// Repositorio de usuarios en memoria para probar casos de uso. Imita la unicidad del nombre y el
/// contador de bloqueo; la fidelidad con SQLite se prueba en Pos.Infrastructure.Tests.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = [];

    public int SaveCount { get; private set; }

    public int AttemptRecords { get; private set; }

    public IReadOnlyCollection<User> All => _users.Values;

    public User Seed(User user)
    {
        _users[user.Id] = user;
        return user;
    }

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> FindByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.SingleOrDefault(u => u.NormalizedUserName == normalizedUserName));

    public Task<bool> AnyRealUserAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.Any(u => !u.IsSystem));

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.Count(u => u is { Role: UserRole.Admin, IsActive: true, IsSystem: false }));

    public Task<UserPage> SearchAsync(UserSearch search, CancellationToken cancellationToken)
    {
        var users = _users.Values.Where(u => !u.IsSystem && (search.IncludeInactive || u.IsActive));
        if (!string.IsNullOrWhiteSpace(search.NameText))
        {
            users = users.Where(u => TextNormalizer.ForSearch(u.FullName).Contains(search.NameText, StringComparison.Ordinal)
                || TextNormalizer.ForSearch(u.UserName).Contains(search.NameText, StringComparison.Ordinal));
        }

        var items = users.OrderBy(u => u.FullName, StringComparer.Ordinal)
            .Select(u => new UserListItem(u.Id, u.FullName, u.UserName, u.Role, u.IsActive, u.Version))
            .ToList();
        return Task.FromResult(new UserPage(items, items.Count, 1, search.PageSize));
    }

    public Task<IReadOnlyList<UserOption>> ListCashiersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserOption>>(
            [.. _users.Values.Where(u => !u.IsSystem).Select(u => new UserOption(u.Id, u.UserName))]);

    public void Add(User user) => _users[user.Id] = user;

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        var duplicated = _users.Values.GroupBy(u => u.NormalizedUserName).Any(g => g.Count() > 1);
        return Task.FromResult(duplicated ? SaveOutcome.Duplicate(UserFields.UserName) : SaveOutcome.Saved);
    }

    public Task RecordLoginAttemptAsync(Guid id, int failedCount, DateTime? lockoutEndsAt, DateTime? lastLoginAt, CancellationToken cancellationToken)
    {
        // La entidad de memoria ya tiene el contador que calculó el dominio; solo se cuenta la escritura.
        AttemptRecords++;
        return Task.CompletedTask;
    }
}
