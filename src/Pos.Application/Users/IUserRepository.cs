using Pos.Application.Products;
using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>Persistencia del agregado Usuario. Específico del agregado; no hay repositorios genéricos.</summary>
public interface IUserRepository
{
    /// <summary>Usuario por id, con seguimiento; incluye a "Sistema".</summary>
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Usuario por nombre ya normalizado, con seguimiento; incluye a "Sistema".</summary>
    Task<User?> FindByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken);

    /// <summary>Indica si existe algún usuario distinto de "Sistema".</summary>
    Task<bool> AnyRealUserAsync(CancellationToken cancellationToken);

    /// <summary>Administradores activos, sin "Sistema".</summary>
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken);

    Task<UserPage> SearchAsync(UserSearch search, CancellationToken cancellationToken);

    /// <summary>Usuarios con ventas o activos, sin "Sistema", para el filtro de ventas.</summary>
    Task<IReadOnlyList<UserOption>> ListCashiersAsync(CancellationToken cancellationToken);

    void Add(User user);

    /// <summary><see cref="SaveStatus.Duplicate"/> por el índice único del nombre; <see cref="SaveStatus.Conflict"/> por versión.</summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Actualiza el contador de bloqueo sin cambiar versión ni auditoría de fila.</summary>
    Task RecordLoginAttemptAsync(
        Guid id,
        int failedCount,
        DateTime? lockoutEndsAt,
        DateTime? lastLoginAt,
        CancellationToken cancellationToken);
}
