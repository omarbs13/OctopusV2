namespace Pos.Domain.Common;

/// <summary>Regla de formato de email compartida por clientes (014) y proveedores (020).</summary>
public static class EmailAddress
{
    /// <summary>Formato básico: una arroba con texto antes y un dominio con punto, sin espacios.</summary>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var text = email.Trim();
        var at = text.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != text.LastIndexOf('@') || text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var domain = text[(at + 1)..];
        var dot = domain.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && !domain.EndsWith('.') && !domain.Contains("..", StringComparison.Ordinal);
    }
}
