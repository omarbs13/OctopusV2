using Pos.Domain.Users;

namespace Pos.Desktop.Navigation;

/// <summary>
/// Árbol del menú construido con lo que registró cada módulo (máximo dos niveles). Una
/// configuración inválida es un error de programación y se detecta al construirlo.
/// </summary>
public sealed class NavigationRegistry
{
    private readonly Dictionary<string, NavigationEntry> _entries;
    private readonly Dictionary<string, NavigationGroup> _groups;

    /// <param name="groups">Grupos registrados.</param>
    /// <param name="entries">Opciones registradas.</param>
    /// <param name="isAllowed">
    /// Decide si el usuario conectado tiene un permiso; las opciones sin permiso permitido y los grupos
    /// que se quedan vacíos no aparecen (FR-008). Nulo permite todo.
    /// </param>
    public NavigationRegistry(
        IEnumerable<NavigationGroup> groups,
        IEnumerable<NavigationEntry> entries,
        Func<Permission, bool>? isAllowed = null)
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
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!ids.Add(entry.Id))
            {
                throw new InvalidOperationException($"La opción de navegación '{entry.Id}' está registrada más de una vez.");
            }

            if (entry.GroupId is not null && !_groups.ContainsKey(entry.GroupId))
            {
                throw new InvalidOperationException(
                    $"La opción '{entry.Id}' pertenece al grupo '{entry.GroupId}', que no está registrado.");
            }

            if (entry.Permission is not { } required || isAllowed is null || isAllowed(required))
            {
                _entries.Add(entry.Id, entry);
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
