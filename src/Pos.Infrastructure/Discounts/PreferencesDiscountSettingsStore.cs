using Pos.Application.Abstractions;
using Pos.Application.Discounts;

namespace Pos.Infrastructure.Discounts;

/// <summary>Guarda el límite de descuento en las preferencias locales (clave <c>discounts</c>, 015 research §10).</summary>
public sealed class PreferencesDiscountSettingsStore : IDiscountSettingsStore
{
    public const string Key = "discounts";

    private readonly IPreferencesStore _preferences;

    public PreferencesDiscountSettingsStore(IPreferencesStore preferences) => _preferences = preferences;

    public DiscountSettings Load()
    {
        var settings = _preferences.Load<DiscountSettings>(Key);
        return settings is { LimitBasisPoints: >= DiscountSettings.MinLimitBasisPoints and <= DiscountSettings.MaxLimitBasisPoints }
            ? settings
            : new DiscountSettings();
    }

    public void Save(DiscountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _preferences.Save(Key, settings);
    }
}
