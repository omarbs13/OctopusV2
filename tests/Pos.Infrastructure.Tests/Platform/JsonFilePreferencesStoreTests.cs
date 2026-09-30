using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Platform;

public sealed class JsonFilePreferencesStoreTests : IDisposable
{
    private readonly TempDataDirectory _dir = new();
    private readonly ListLogger<JsonFilePreferencesStore> _logger = new();

    public void Dispose() => _dir.Dispose();

    private JsonFilePreferencesStore CreateStore() => new(_dir.Paths, _logger);

    public sealed record Sample(bool Collapsed, string[] ExpandedGroups);

    [Fact]
    public void GuardarYLeer_DevuelveElMismoValor()
    {
        var store = CreateStore();

        store.Save("navigation", new Sample(true, ["catalogs", "inventory"]));
        var loaded = CreateStore().Load<Sample>("navigation");

        Assert.NotNull(loaded);
        Assert.True(loaded.Collapsed);
        Assert.Equal(["catalogs", "inventory"], loaded.ExpandedGroups);
        Assert.True(File.Exists(Path.Combine(_dir.Paths.PreferencesDirectory, "navigation.json")));
        Assert.Empty(Directory.GetFiles(_dir.Paths.PreferencesDirectory, "*.tmp"));
    }

    [Fact]
    public void ClaveInexistente_EsNula()
    {
        Assert.Null(CreateStore().Load<Sample>("noexiste"));
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public void JsonDaniado_EsNuloYAdvierte()
    {
        Directory.CreateDirectory(_dir.Paths.PreferencesDirectory);
        File.WriteAllText(Path.Combine(_dir.Paths.PreferencesDirectory, "navigation.json"), "{ esto no es json");

        Assert.Null(CreateStore().Load<Sample>("navigation"));
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public void Formato_UsaNombresEnMinuscula()
    {
        CreateStore().Save("navigation", new Sample(false, ["help"]));

        var json = File.ReadAllText(Path.Combine(_dir.Paths.PreferencesDirectory, "navigation.json"));

        Assert.Contains("\"collapsed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"expandedGroups\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardarSinPermisos_NoLanzaYAdvierte()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Los permisos POSIX solo se simulan en Linux.");
        Assert.SkipWhen(Environment.UserName == "root", "root ignora los permisos de archivo.");

        Directory.CreateDirectory(_dir.Paths.PreferencesDirectory);
        UnixPermissions.MakeReadOnly(_dir.Paths.PreferencesDirectory);
        try
        {
            CreateStore().Save("navigation", new Sample(true, []));
        }
        finally
        {
            UnixPermissions.MakeWritable(_dir.Paths.PreferencesDirectory);
        }

        Assert.Single(_logger.Warnings);
    }
}

/// <summary>Logger que guarda las advertencias emitidas.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (logLevel == LogLevel.Warning)
        {
            Warnings.Add(formatter(state, exception));
        }
    }
}
