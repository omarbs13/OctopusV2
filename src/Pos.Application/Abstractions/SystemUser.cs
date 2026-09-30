namespace Pos.Application.Abstractions;

/// <summary>
/// Usuario "Sistema": dueño de los registros anteriores a los usuarios (007) y usuario de las
/// operaciones sin sesión (arranque, intentos de acceso). No puede iniciar sesión.
/// </summary>
public static class SystemUser
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public const string DisplayName = "Sistema";

    /// <summary>
    /// Nombre para mostrar de un usuario: "Sistema" o, hasta que exista el módulo de usuarios, los
    /// primeros 8 caracteres de su id.
    /// </summary>
    public static string NameOf(Guid userId) =>
        userId == Id ? DisplayName : userId.ToString("N")[..8];
}
