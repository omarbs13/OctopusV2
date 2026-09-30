namespace Pos.Domain.Users;

/// <summary>Reglas del nombre de usuario: de 3 a 40 caracteres, solo letras, dígitos, ".", "_" y "-", sin espacios.</summary>
public static class UserNameRules
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 40;

    /// <summary>Forma comparable del nombre: sin espacios en los extremos y en mayúsculas invariantes (FR-003).</summary>
    public static string Normalize(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return userName.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? userName)
    {
        if (userName is null)
        {
            return false;
        }

        var text = userName.Trim();
        return text.Length is >= UserNameMinLength and <= UserNameMaxLength && text.All(IsAllowed);
    }

    private static bool IsAllowed(char c) => char.IsLetterOrDigit(c) || c is '.' or '_' or '-';
}
