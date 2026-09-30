using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;

namespace Pos.Desktop.Forms;

/// <summary>
/// Base de todo formulario (contracts/forms.md). Su comportamiento no depende de cómo se muestra:
/// Guardar evita el doble clic, los cambios se detectan comparando el estado normalizado y la
/// validación ocurre solo al guardar.
/// </summary>
public abstract partial class FormViewModel : ViewModelBase
{
    private object? _originalState;

    protected FormViewModel(IDialogService dialogs) => Dialogs = dialogs;

    /// <summary>El formulario terminó (guardado o cerrado) y su contenedor debe quitarlo.</summary>
    public event EventHandler? Completed;

    /// <summary>El formulario se cerró sin guardar.</summary>
    public event EventHandler? Closed;

    public abstract string Title { get; }

    /// <summary>Campo que debe recibir el foco (el primero con error).</summary>
    [ObservableProperty]
    public partial string? FocusField { get; set; }

    /// <summary>Algún valor difiere del original, comparado tal como se guardaría.</summary>
    public bool IsDirty => !Equals(_originalState, CaptureState());

    protected IDialogService Dialogs { get; }

    /// <summary>Registro inmutable con los valores normalizados como se guardarían.</summary>
    protected abstract object CaptureState();

    /// <summary>Valida con el caso de uso, muestra los errores y guarda. Devuelve verdadero si se guardó.</summary>
    protected abstract Task<bool> SaveCoreAsync();

    /// <summary>Toma los valores actuales como originales (al abrir, al recargar y después de guardar).</summary>
    protected void ResetOriginalState() => _originalState = CaptureState();

    /// <summary>
    /// Verdadero si se puede salir del formulario. Con cambios pregunta Guardar, Descartar o Seguir
    /// editando; Guardar con errores o rechazado deja el formulario abierto (FR-028 a FR-030).
    /// </summary>
    public virtual async Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        return await Dialogs.AskUnsavedChangesAsync() switch
        {
            UnsavedChangesChoice.Save => await SaveCoreAsync(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    protected void RaiseClosed()
    {
        Closed?.Invoke(this, EventArgs.Empty);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    protected void RaiseSavedCompleted()
    {
        ResetOriginalState();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveCoreAsync();

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (await ConfirmLeaveAsync())
        {
            RaiseClosed();
        }
    }
}

/// <summary>Formulario cuyo guardado produce un resultado (por ejemplo, el producto guardado).</summary>
public abstract class FormViewModel<TResult> : FormViewModel
{
    protected FormViewModel(IDialogService dialogs)
        : base(dialogs)
    {
    }

    public event EventHandler<TResult>? Saved;

    /// <summary>Lo llama el formulario al guardar con éxito.</summary>
    protected void OnSaved(TResult result)
    {
        Saved?.Invoke(this, result);
        RaiseSavedCompleted();
    }
}
