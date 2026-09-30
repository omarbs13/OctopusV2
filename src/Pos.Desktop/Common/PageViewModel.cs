using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;

namespace Pos.Desktop.Common;

/// <summary>Pantalla navegable desde el menú lateral.</summary>
public abstract class PageViewModel : ViewModelBase, ILeaveGuard
{
    public abstract string Title { get; }

    /// <summary>Formularios de la pantalla, si los tiene.</summary>
    public virtual FormHost? Forms => null;

    /// <summary>Se invoca cada vez que la pantalla pasa a ser la actual.</summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;

    /// <summary>Se puede salir si no hay formulario abierto o si el formulario lo permite.</summary>
    public virtual Task<bool> CanLeaveAsync() => Forms?.CloseActiveAsync() ?? Task.FromResult(true);
}
