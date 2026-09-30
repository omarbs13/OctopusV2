using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Platform;

/// <summary>Un archivo JSON por clave en &lt;datos&gt;/preferences/; se escribe como .tmp y se renombra.</summary>
public sealed partial class JsonFilePreferencesStore : IPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IAppPaths _paths;
    private readonly ILogger<JsonFilePreferencesStore> _logger;

    public JsonFilePreferencesStore(IAppPaths paths, ILogger<JsonFilePreferencesStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public T? Load<T>(string key)
        where T : class
    {
        var file = FileFor(key);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogUnreadable(ex, file);
            return null;
        }
    }

    public void Save<T>(string key, T value)
        where T : class
    {
        var file = FileFor(key);
        var temporary = file + ".tmp";
        try
        {
            Directory.CreateDirectory(_paths.PreferencesDirectory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(temporary, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogNotSaved(ex, file);
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // El temporal no se pudo borrar; no afecta al operador.
            }
        }
    }

    private string FileFor(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Path.Combine(_paths.PreferencesDirectory, $"{key}.json");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo leer la preferencia {File}; se usan los valores predeterminados")]
    private partial void LogUnreadable(Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar la preferencia {File}")]
    private partial void LogNotSaved(Exception exception, string file);
}
