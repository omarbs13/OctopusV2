using Pos.Application.Abstractions;
using Pos.Application.Security;

namespace Pos.Infrastructure.Security;

/// <summary>Guarda la configuración de seguridad en las preferencias locales (clave <c>security</c>).</summary>
public sealed class PreferencesSecuritySettingsStore : ISecuritySettingsStore
{
    public const string Key = "security";

    private readonly IPreferencesStore _preferences;

    public PreferencesSecuritySettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public SecuritySettings Load() => _preferences.Load<SecuritySettings>(Key) ?? new SecuritySettings();

    public void Save(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
