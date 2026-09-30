namespace Pos.Desktop.Navigation;

/// <summary>
/// Árbol del menú construido con lo que registró cada módulo (máximo dos niveles). Una
/// configuración inválida es un error de programación y se detecta al construirlo.
/// </summary>
public sealed class NavigationRegistry
{
    private readonly Dictionary<string, NavigationEntry> _entries;
    private readonly Dictionary<string, NavigationGroup> _groups;

    public NavigationRegistry(IEnumerable<NavigationGroup> groups, IEnumerable<NavigationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(entries);

        _groups = [];
        foreach (var group in groups)
        {
            if (!_groups.TryAdd(group.Id, group))
            {
                throw new InvalidOperationException($"El grupo de navegación '{group.Id}' está registrado más de una vez.");
            }
        }

        _entries = [];
        foreach (var entry in entries)
        {
            if (!_entries.TryAdd(entry.Id, entry))
            {
                throw new InvalidOperationException($"La opción de navegación '{entry.Id}' está registrada más de una vez.");
            }

            if (entry.GroupId is not null && !_groups.ContainsKey(entry.GroupId))
            {
                throw new InvalidOperationException(
                    $"La opción '{entry.Id}' pertenece al grupo '{entry.GroupId}', que no está registrado.");
            }
        }

        Entries = [.. _entries.Values.OrderBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal)];

        var topLevel = Entries.Where(e => e.GroupId is null).Select(e => new NavigationNode(null, e, []));
        var grouped = _groups.Values
            .Select(g => new NavigationNode(g, null, [.. Entries.Where(e => e.GroupId == g.Id)]))
            .Where(n => n.Children.Count > 0);

        Roots = [.. topLevel.Concat(grouped).OrderBy(n => n.Order).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    public IReadOnlyList<NavigationNode> Roots { get; }

    public IReadOnlyList<NavigationEntry> Entries { get; }

    public NavigationEntry? FindEntry(string id) => _entries.GetValueOrDefault(id);

    public NavigationGroup? GroupOf(string entryId) =>
        FindEntry(entryId)?.GroupId is { } groupId ? _groups[groupId] : null;
}
