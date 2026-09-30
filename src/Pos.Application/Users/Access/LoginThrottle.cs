using System.Collections.Concurrent;
using Pos.Application.Abstractions;
using Pos.Domain.Users;

namespace Pos.Application.Users.Access;

/// <summary>
/// Contador en memoria por nombre de usuario inexistente: aplica la misma regla de 5 intentos y 5
/// minutos para no revelar que el usuario no existe (007, research §9). Se pierde al reiniciar, lo
/// cual es aceptable porque no protege ninguna cuenta real.
/// </summary>
public sealed class LoginThrottle
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private string? _dummyHash;

    public LoginThrottle(IClock clock) => _clock = clock;

    /// <summary>
    /// Hash ficticio con el costo vigente: se verifica contra él cuando el usuario no existe para
    /// que la respuesta tarde lo mismo y no revele la inexistencia.
    /// </summary>
    public string DummyHashFor(IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);
        return _dummyHash ??= hasher.Hash("contraseña-ficticia");
    }

    /// <summary>Fin del bloqueo vigente del nombre, o nulo.</summary>
    public DateTime? LockedUntil(string normalizedName)
    {
        var now = _clock.UtcNow;
        return _entries.TryGetValue(normalizedName, out var entry) && entry.LockoutEndsAt is { } end && end > now ? end : null;
    }

    /// <summary>Suma un fallo; devuelve <c>true</c> si con este fallo el nombre queda bloqueado.</summary>
    public bool RegisterFailure(string normalizedName)
    {
        var now = _clock.UtcNow;
        var locked = false;
        _entries.AddOrUpdate(
            normalizedName,
            _ =>
            {
                locked = User.MaxFailedAttempts <= 1;
                return new Entry(1, locked ? now + User.LockoutDuration : null);
            },
            (_, current) =>
            {
                var count = current.LockoutEndsAt is { } end && end <= now ? 1 : current.Count + 1;
                locked = count >= User.MaxFailedAttempts;
                return new Entry(count, locked ? now + User.LockoutDuration : null);
            });
        return locked;
    }

    private sealed record Entry(int Count, DateTime? LockoutEndsAt);
}
