using Pos.Application.Users;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Hasher rápido de pruebas: no es seguro, solo evita el costo de PBKDF2.</summary>
public sealed class FastPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"fast${password}";

    public PasswordCheck Verify(string password, string storedHash) =>
        storedHash == $"fast${password}" ? PasswordCheck.Succeeded
        : storedHash == $"old${password}" ? PasswordCheck.SucceededRehashNeeded
        : PasswordCheck.Failed;
}
