namespace Pos.Application.Users;

public enum PasswordCheck
{
    Failed,
    Succeeded,

    /// <summary>La contraseña es correcta pero el hash usa menos iteraciones que las vigentes.</summary>
    SucceededRehashNeeded,
}

/// <summary>Hash robusto de contraseñas (Principio IX); nunca se guardan en claro.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string password, string storedHash);
}
