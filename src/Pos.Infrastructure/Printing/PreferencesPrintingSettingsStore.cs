using Pos.Application.Abstractions;
using Pos.Application.Printing;

namespace Pos.Infrastructure.Printing;

/// <summary>Guarda la configuración de impresión en las preferencias locales (clave <c>printing</c>).</summary>
public sealed class PreferencesPrintingSettingsStore : IPrintingSettingsStore
{
    public const string Key = "printing";

    private readonly IPreferencesStore _preferences;

    public PreferencesPrintingSettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public PrintingSettings Load() => _preferences.Load<PrintingSettings>(Key) ?? new PrintingSettings();

    public void Save(PrintingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
