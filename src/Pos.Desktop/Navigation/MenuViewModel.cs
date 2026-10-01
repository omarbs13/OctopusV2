using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;

namespace Pos.Desktop.Navigation;

/// <summary>Preferencias del menú que se recuerdan entre sesiones (FR-017).</summary>
public sealed record NavigationPreferences(bool Collapsed, string[] ExpandedGroups);

/// <summary>Opción o grupo del menú lateral.</summary>
public sealed partial class MenuItemViewModel : ViewModelBase
{
    private readonly Func<MenuItemViewModel, Task> _activate;

    public MenuItemViewModel(
        string id,
        string title,
        string icon,
        Func<MenuItemViewModel, Task> activate,
        IReadOnlyList<MenuItemViewModel>? children = null,
        string? shortcut = null)
    {
        Id = id;
        Shortcut = shortcut;
        Title = title;
        Icon = icon;
        _activate = activate;
        Children = children ?? [];
    }

    public string Id { get; }

    public string Title { get; }

    public string Icon { get; }

    /// <summary>Atajo global de la opción, por ejemplo "F9"; nulo si no tiene.</summary>
    public string? Shortcut { get; }

    public IReadOnlyList<MenuItemViewModel> Children { get; }

    public bool IsGroup => Children.Count > 0;

    /// <summary>Grupo abierto (menú expandido).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChildren))]
    public partial bool IsExpanded { get; set; }

    /// <summary>El menú completo está contraído (solo íconos).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFull), nameof(ShowIconOnly), nameof(ShowFlyout), nameof(ShowChildren))]
    public partial bool IsMenuCollapsed { get; set; }

    /// <summary>Menú expandido: ícono y texto.</summary>
    public bool ShowFull => !IsMenuCollapsed;

    /// <summary>Menú contraído, opción suelta: solo ícono.</summary>
    public bool ShowIconOnly => IsMenuCollapsed && !IsGroup;

    /// <summary>Menú contraído, grupo: ícono con menú flotante.</summary>
    public bool ShowFlyout => IsMenuCollapsed && IsGroup;

    /// <summary>Menú expandido y grupo abierto: se ven sus opciones.</summary>
    public bool ShowChildren => !IsMenuCollapsed && IsGroup && IsExpanded;

    /// <summary>Opción actual.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Grupo que contiene la opción actual.</summary>
    [ObservableProperty]
    public partial bool IsCurrentGroup { get; set; }

    /// <summary>Opción: navega. Grupo: abre o cierra (menú expandido).</summary>
    [RelayCommand]
    private Task ActivateAsync() => _activate(this);
}

/// <summary>
/// Menú lateral de dos niveles, colapsable, con preferencias persistentes y contracción automática
/// en ventanas angostas (FR-012 a FR-018). La elección del operador se guarda; la contracción
/// automática nunca se guarda.
/// </summary>
public sealed partial class MenuViewModel : ViewModelBase, IDisposable
{
    public const string PreferencesKey = "navigation";
    public const double AutoCollapseWidth = 1000;

    private readonly NavigationRegistry _registry;
    private readonly Navigator _navigator;
    private readonly IPreferencesStore _preferences;
    private readonly ILicenseState? _license;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly bool _hasSavedPreferences;

    public MenuViewModel(
        NavigationRegistry registry,
        Navigator navigator,
        IPreferencesStore preferences,
        UserSectionViewModel? userSection = null,
        ILicenseState? license = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(navigator);
        _registry = registry;
        _navigator = navigator;
        _preferences = preferences;
        _license = license;
        UserSection = userSection;

        Items = BuildItems();

        var saved = preferences.Load<NavigationPreferences>(PreferencesKey);
        if (saved is not null)
        {
            _hasSavedPreferences = true;
            IsUserCollapsed = saved.Collapsed;
            foreach (var group in Groups.Where(g => saved.ExpandedGroups.Contains(g.Id)))
            {
                group.IsExpanded = true;
            }
        }

        _navigator.CurrentChanged += (_, _) => SyncCurrent();
        SyncCurrent();
        SyncCollapsed();

        if (_license is not null)
        {
            _license.Changed += OnLicenseChanged;
        }
    }

    /// <summary>Opciones visibles; se reconstruye al cambiar la licencia, sin reiniciar (012, FR-017).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MenuItemViewModel> Items { get; private set; }

    public void Dispose()
    {
        if (_license is not null)
        {
            _license.Changed -= OnLicenseChanged;
        }
    }

    /// <summary>Sección del usuario conectado al pie del menú; nula si no hay sesión (pruebas).</summary>
    public UserSectionViewModel? UserSection { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    public partial bool IsUserCollapsed { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    public partial bool IsAutoCollapsed { get; private set; }

    public bool IsCollapsed => IsUserCollapsed || IsAutoCollapsed;

    private IEnumerable<MenuItemViewModel> Groups => Items.Where(i => i.IsGroup);

    /// <summary>Informa el ancho de la ventana; por debajo del umbral el menú se contrae solo.</summary>
    public void SetWindowWidth(double width) => IsAutoCollapsed = width < AutoCollapseWidth;

    [RelayCommand]
    private void Toggle()
    {
        IsUserCollapsed = !IsCollapsed;
        IsAutoCollapsed = false;
        SavePreferences();
    }

    [RelayCommand]
    private void ToggleGroup(MenuItemViewModel? group)
    {
        if (group is { IsGroup: true })
        {
            group.IsExpanded = !group.IsExpanded;
            SavePreferences();
        }
    }

    [RelayCommand]
    private async Task SelectEntryAsync(MenuItemViewModel? item)
    {
        if (item is { IsGroup: false })
        {
            await _navigator.NavigateAsync(item.Id);
        }
    }

    private IReadOnlyList<MenuItemViewModel> BuildItems() =>
        [.. _registry.Roots.Select(node => node.Group is { } group
            ? new MenuItemViewModel(group.Id, group.Title, group.Icon, ActivateAsync, [.. node.Children.Select(ToItem)])
            : ToItem(node.Entry!))];

    private void OnLicenseChanged(object? sender, EventArgs e)
    {
        if (_context is null)
        {
            RebuildItems();
        }
        else
        {
            _context.Post(_ => RebuildItems(), null);
        }
    }

    private void RebuildItems()
    {
        var expanded = Groups.Where(g => g.IsExpanded).Select(g => g.Id).ToHashSet();
        _registry.Rebuild();
        var items = BuildItems();
        foreach (var group in items.Where(i => i.IsGroup && expanded.Contains(i.Id)))
        {
            group.IsExpanded = true;
        }

        Items = items;
        SyncCurrent();
        SyncCollapsed();
    }

    private MenuItemViewModel ToItem(NavigationEntry entry) =>
        new(entry.Id, entry.Title, entry.Icon, ActivateAsync, shortcut: entry.Shortcut);

    private Task ActivateAsync(MenuItemViewModel item)
    {
        if (item.IsGroup)
        {
            ToggleGroup(item);
            return Task.CompletedTask;
        }

        return SelectEntryAsync(item);
    }

    partial void OnIsUserCollapsedChanged(bool value) => SyncCollapsed();

    partial void OnIsAutoCollapsedChanged(bool value) => SyncCollapsed();

    private void SyncCollapsed()
    {
        foreach (var item in Items)
        {
            item.IsMenuCollapsed = IsCollapsed;
        }
    }

    private void SavePreferences() =>
        _preferences.Save(
            PreferencesKey,
            new NavigationPreferences(IsUserCollapsed, [.. Groups.Where(g => g.IsExpanded).Select(g => g.Id)]));

    private void SyncCurrent()
    {
        var current = _navigator.CurrentEntryId;
        foreach (var item in Items)
        {
            item.IsCurrent = !item.IsGroup && item.Id == current;
            foreach (var child in item.Children)
            {
                child.IsCurrent = child.Id == current;
            }

            item.IsCurrentGroup = item.IsGroup && item.Children.Any(c => c.IsCurrent);

            // Sin preferencias guardadas, se abre el grupo de la opción actual (sin guardarlo).
            if (!_hasSavedPreferences && item.IsCurrentGroup)
            {
                item.IsExpanded = true;
            }
        }
    }
}
