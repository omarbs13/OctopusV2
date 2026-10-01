using Pos.Application.Abstractions;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Common;

public class ThemeServiceTests
{
    [Fact]
    public void ElegirTema_GuardaLaPreferenciaEnElArchivo()
    {
        var store = new MemoryPreferences();

        new ThemeService(store).Set(AppTheme.Dark);

        Assert.Equal("Dark", store.Saved[ThemeService.PreferenceKey].Theme);
    }

    [Theory]
    [InlineData("Light", AppTheme.Light)]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("dark", null)]
    [InlineData("Otro", null)]
    [InlineData(null, null)]
    public void ValorGuardado_SoloAceptaLosTemasConocidos(string? value, AppTheme? expected) =>
        Assert.Equal(expected, ThemeService.Parse(value));

    private sealed class MemoryPreferences : IPreferencesStore
    {
        public Dictionary<string, ThemePreference> Saved { get; } = [];

        public T? Load<T>(string key)
            where T : class => Saved.TryGetValue(key, out var value) ? value as T : null;

        public void Save<T>(string key, T value)
            where T : class => Saved[key] = (ThemePreference)(object)value;
    }
}
