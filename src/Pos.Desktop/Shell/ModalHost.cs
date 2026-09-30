using CommunityToolkit.Mvvm.ComponentModel;

namespace Pos.Desktop.Shell;

/// <summary>
/// Diálogo sobre toda la sesión (cambio de contraseña, autorización de administrador). Es de la
/// sesión: al cerrarla se desecha junto con el resto del ámbito.
/// </summary>
public sealed partial class ModalHost : ObservableObject
{
    /// <summary>ViewModel del diálogo abierto, o nulo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial object? Content { get; private set; }

    public bool IsOpen => Content is not null;

    public void Show(object content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    public void Close() => Content = null;
}
