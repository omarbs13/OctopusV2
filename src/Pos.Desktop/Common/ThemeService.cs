using Avalonia.Styling;
using Pos.Application.Abstractions;

namespace Pos.Desktop.Common;

/// <summary>Tema elegido por el usuario; sin preferencia guardada la aplicación sigue al sistema.</summary>
public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>Contenido del archivo <c>theme.json</c> de la carpeta de preferencias (no se guarda en la base de datos).</summary>
public sealed class ThemePreference
{
    /// <summary>"Light" o "Dark"; cualquier otro valor se ignora.</summary>
    public string? Theme { get; set; }
}

/// <summary>
/// Aplica y guarda el tema claro u oscuro. La preferencia vive en un archivo JSON de la carpeta de datos
/// (<see cref="IPreferencesStore"/>); se aplica al arrancar con <see cref="ApplySaved"/> y al cambiar con
/// <see cref="Set"/>. Un archivo inexistente o dañado deja el tema del sistema.
/// </summary>
public sealed class ThemeService
{
    public const string PreferenceKey = "theme";

    private readonly IPreferencesStore _preferences;

    public ThemeService(IPreferencesStore preferences) => _preferences = preferences;

    /// <summary>Tema que se está mostrando (el elegido o, sin elección, el del sistema).</summary>
    public static AppTheme Current =>
        (Avalonia.Application.Current?.ActualThemeVariant ?? ThemeVariant.Light) == ThemeVariant.Dark ? AppTheme.Dark : AppTheme.Light;

    /// <summary>Lee la preferencia guardada y la aplica; sin preferencia válida no cambia nada.</summary>
    public void ApplySaved()
    {
        if (Parse(_preferences.Load<ThemePreference>(PreferenceKey)?.Theme) is { } theme)
        {
            Apply(theme);
        }
    }

    /// <summary>Aplica el tema y lo guarda; un error al guardar se registra y no interrumpe.</summary>
    public void Set(AppTheme theme)
    {
        Apply(theme);
        _preferences.Save(PreferenceKey, new ThemePreference { Theme = theme.ToString() });
    }

    internal static AppTheme? Parse(string? value) =>
        Enum.TryParse<AppTheme>(value, ignoreCase: false, out var theme) && Enum.IsDefined(theme) ? theme : null;

    private static void Apply(AppTheme theme)
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
