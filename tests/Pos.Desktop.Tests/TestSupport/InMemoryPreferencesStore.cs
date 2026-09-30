using Pos.Application.Abstractions;

namespace Pos.Desktop.Tests.TestSupport;

public sealed class InMemoryPreferencesStore : IPreferencesStore
{
    public Dictionary<string, object> Values { get; } = [];

    public int SaveCount { get; private set; }

    public T? Load<T>(string key)
        where T : class => Values.TryGetValue(key, out var value) ? value as T : null;

    public void Save<T>(string key, T value)
        where T : class
    {
        SaveCount++;
        Values[key] = value;
    }
}
