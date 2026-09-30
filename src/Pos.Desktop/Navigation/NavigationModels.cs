using Avalonia.Controls;

namespace Pos.Desktop.Navigation;

/// <summary>Grupo del menú lateral (segundo nivel máximo).</summary>
public sealed record NavigationGroup(string Id, string Title, string Icon, int Order);

/// <summary>
/// Opción navegable; sin grupo aparece en el primer nivel del menú. <paramref name="Resolve"/> permite
/// que varias opciones compartan el tipo de ViewModel con instancias propias.
/// </summary>
public sealed record NavigationEntry(
    string Id,
    string Title,
    string Icon,
    int Order,
    string? GroupId,
    Type ViewModelType,
    Func<IServiceProvider, object>? Resolve = null);

/// <summary>Asociación de un tipo de ViewModel con la fábrica de su vista.</summary>
public sealed record ViewRegistration(Type ViewModelType, Func<Control> CreateView);

/// <summary>Nodo de primer nivel del menú: una opción suelta o un grupo con sus opciones.</summary>
public sealed record NavigationNode(NavigationGroup? Group, NavigationEntry? Entry, IReadOnlyList<NavigationEntry> Children)
{
    public string Id => Group?.Id ?? Entry!.Id;

    public int Order => Group?.Order ?? Entry!.Order;
}

/// <summary>Una pantalla que puede impedir que se salga de ella (por ejemplo, con cambios sin guardar).</summary>
public interface ILeaveGuard
{
    Task<bool> CanLeaveAsync();
}
