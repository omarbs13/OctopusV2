using Pos.Application.Abstractions;
using Pos.Application.Returns;

namespace Pos.Infrastructure.Returns;

/// <summary>Guarda el plazo de devoluciones en las preferencias locales (clave <c>returns</c>).</summary>
public sealed class PreferencesReturnsSettingsStore : IReturnsSettingsStore
{
    public const string Key = "returns";

    private readonly IPreferencesStore _preferences;

    public PreferencesReturnsSettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public ReturnsSettings Load()
    {
        var settings = _preferences.Load<ReturnsSettings>(Key);
        return settings is { ReturnWindowDays: >= ReturnsSettings.MinReturnWindowDays and <= ReturnsSettings.MaxReturnWindowDays }
            ? settings
            : new ReturnsSettings();
    }

    public void Save(ReturnsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
