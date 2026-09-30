namespace Pos.Desktop.Shell;

/// <summary>Acciones sobre la sesión que ofrece el menú del usuario; las implementa <see cref="RootViewModel"/>.</summary>
public interface ISessionActions
{
    /// <summary>Cierra la sesión y vuelve al inicio de sesión; falso si el operador lo canceló.</summary>
    Task<bool> LogoutAsync();

    /// <summary>Igual que cerrar sesión, para entrar con otro usuario.</summary>
    Task<bool> SwitchUserAsync();
}

/// <summary>Navegación de la sesión actual para servicios que no pertenecen a ella (por ejemplo, la impresión).</summary>
public interface ISessionNavigation
{
    Task<bool> NavigateAsync(string entryId);
}
