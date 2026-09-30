using System.Security.Cryptography;
using Pos.Application.Users;

namespace Pos.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 del runtime (Principio IX, research §1). Formato guardado:
/// <c>pbkdf2-sha256$&lt;iteraciones&gt;$&lt;sal base64&gt;$&lt;hash base64&gt;</c>.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int CurrentIterations = 600_000;

    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher()
        : this(CurrentIterations)
    {
    }

    /// <summary>Permite fijar las iteraciones vigentes; las pruebas de reactivación usan un valor distinto.</summary>
    public Pbkdf2PasswordHasher(int iterations) => _iterations = iterations;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordCheck Verify(string password, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);

        var parts = storedHash.Split('$');
        if (parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            return PasswordCheck.Failed;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordCheck.Failed;
        }

        return iterations < _iterations ? PasswordCheck.SucceededRehashNeeded : PasswordCheck.Succeeded;
    }
}
