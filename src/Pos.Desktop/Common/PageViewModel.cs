namespace Pos.Desktop.Common;

/// <summary>Pantalla navegable desde el menú lateral.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }

    /// <summary>Se invoca cada vez que la pantalla pasa a ser la actual.</summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;
}
