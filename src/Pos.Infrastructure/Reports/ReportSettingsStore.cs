using Pos.Application.Abstractions;
using Pos.Application.Reports;

namespace Pos.Infrastructure.Reports;

/// <summary>Guarda la configuración de reportes en las preferencias locales (clave <c>reports</c>).</summary>
public sealed class PreferencesReportSettingsStore : IReportSettingsStore
{
    public const string Key = "reports";

    private readonly IPreferencesStore _preferences;

    public PreferencesReportSettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public ReportSettings Load() => _preferences.Load<ReportSettings>(Key) ?? new ReportSettings();

    public void Save(ReportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
