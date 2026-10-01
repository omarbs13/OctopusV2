using Pos.Application.Abstractions;
using Pos.Application.Receivables;

namespace Pos.Infrastructure.Receivables;

/// <summary>Guarda el plazo de pago en las preferencias locales (clave <c>receivables</c>).</summary>
public sealed class PreferencesReceivablesSettingsStore : IReceivablesSettingsStore
{
    public const string Key = "receivables";

    private readonly IPreferencesStore _preferences;

    public PreferencesReceivablesSettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public ReceivablesSettings Load()
    {
        var settings = _preferences.Load<ReceivablesSettings>(Key);
        return settings is { PaymentTermDays: >= ReceivablesSettings.MinPaymentTermDays and <= ReceivablesSettings.MaxPaymentTermDays }
            ? settings
            : new ReceivablesSettings();
    }

    public void Save(ReceivablesSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
