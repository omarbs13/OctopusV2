using Pos.Domain.Common;

namespace Pos.Domain.Users;

/// <summary>
/// Persona que opera el sistema, más la fila especial "Sistema". Nunca se borra físicamente
/// (FR-017). Los campos de auditoría los asigna la persistencia.
/// </summary>
public sealed class User
{
    public const int FullNameMaxLength = 100;
    public const int PasswordMinLength = 8;
    public const int PasswordHashMaxLength = 200;
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private User()
    {
        FullName = string.Empty;
        UserName = string.Empty;
        NormalizedUserName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; }

    public string UserName { get; private set; }

    public string NormalizedUserName { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsSystem { get; private set; }

    public string? PasswordHash { get; private set; }

    public bool MustChangePassword { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTime? LockoutEndsAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public static User Create(string fullName, string userName, UserRole role, string passwordHash) =>
        Build(fullName, userName, role, passwordHash, mustChangePassword: true);

    public static User CreateFirstAdmin(string fullName, string userName, string passwordHash) =>
        Build(fullName, userName, UserRole.Admin, passwordHash, mustChangePassword: false);

    public void Rename(string fullName) => FullName = ValidFullName(fullName);

    public void ChangeUserName(string userName)
    {
        UserName = ValidUserName(userName);
        NormalizedUserName = UserNameRules.Normalize(UserName);
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SetPassword(string passwordHash, bool mustChange)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            throw new DomainException("La contraseña protegida no es válida.");
        }

        PasswordHash = passwordHash;
        MustChangePassword = mustChange;
    }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndsAt is { } end && end > nowUtc;

    /// <summary>Suma un fallo; devuelve <c>true</c> si con este fallo el usuario queda bloqueado.</summary>
    public bool RegisterFailedLogin(DateTime nowUtc)
    {
        if (LockoutEndsAt is { } end && end <= nowUtc)
        {
            FailedLoginCount = 0;
            LockoutEndsAt = null;
        }

        FailedLoginCount++;
        if (FailedLoginCount < MaxFailedAttempts)
        {
            return false;
        }

        LockoutEndsAt = nowUtc + LockoutDuration;
        return true;
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        LastLoginAt = nowUtc;
    }

    /// <summary>Restablece el contador de fallos y el bloqueo temporal.</summary>
    public void ClearLockout()
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
    }

    private static User Build(string fullName, string userName, UserRole role, string passwordHash, bool mustChangePassword)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Role = role,
            IsActive = true,
            Version = 1,
        };
        user.Rename(fullName);
        user.ChangeUserName(userName);
        user.SetPassword(passwordHash, mustChangePassword);
        return user;
    }

    private static string ValidFullName(string fullName)
    {
        var text = fullName?.Trim() ?? string.Empty;
        if (text.Length is 0 or > FullNameMaxLength)
        {
            throw new DomainException("El nombre completo no es válido.");
        }

        return text;
    }

    private static string ValidUserName(string userName)
    {
        if (!UserNameRules.IsValid(userName))
        {
            throw new DomainException("El nombre de usuario no es válido.");
        }

        return userName.Trim();
    }
}
