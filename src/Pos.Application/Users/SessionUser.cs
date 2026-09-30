using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>Instantánea del usuario conectado.</summary>
public sealed record SessionUser(Guid Id, string FullName, string UserName, UserRole Role, string Initials)
{
    public static SessionUser From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new SessionUser(user.Id, user.FullName, user.UserName, user.Role, InitialsOf(user.FullName));
    }

    /// <summary>Hasta dos iniciales en mayúsculas de las primeras palabras del nombre.</summary>
    public static string InitialsOf(string fullName)
    {
        var letters = (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0]));
        var text = string.Concat(letters);
        return text.Length == 0 ? "?" : text;
    }
}
