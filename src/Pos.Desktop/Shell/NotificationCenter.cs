using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Navigation;

namespace Pos.Desktop.Shell;

/// <summary>
/// Notificaciones de la sesión (022, research §10): una pila abajo a la derecha que nunca toma el foco
/// ni bloquea la venta. Como máximo una por <see cref="NotificationItem.Key"/>; las de
/// <see cref="NotificationSeverity.Danger"/> van arriba. Permanecen hasta que se pulsan o se descartan.
/// </summary>
public sealed partial class NotificationCenter
{
    private readonly Navigator _navigator;

    public NotificationCenter(Navigator navigator) => _navigator = navigator;

    public ObservableCollection<NotificationItem> Items { get; } = [];

    public void Show(NotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Items.FirstOrDefault(i => i.Key == item.Key) is { } existing)
        {
            Items.Remove(existing);
        }

        var index = item.Severity == NotificationSeverity.Danger
            ? 0
            : Items.Count(i => i.Severity == NotificationSeverity.Danger);
        Items.Insert(index, item);
    }

    /// <summary>Pulsar el cuerpo: cierra la notificación y abre su destino.</summary>
    [RelayCommand]
    private async Task ActivateAsync(NotificationItem? item)
    {
        if (item is null)
        {
            return;
        }

        Items.Remove(item);
        if (item.NavigateTo is { } target)
        {
            await _navigator.NavigateAsync(target, item.Argument);
        }
    }

    /// <summary>Botón "×": cierra la notificación sin navegar.</summary>
    [RelayCommand]
    private void Dismiss(NotificationItem? item)
    {
        if (item is not null)
        {
            Items.Remove(item);
        }
    }
}
