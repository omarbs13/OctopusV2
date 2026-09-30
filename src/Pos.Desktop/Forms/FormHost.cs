using CommunityToolkit.Mvvm.ComponentModel;

namespace Pos.Desktop.Forms;

/// <summary>
/// Formulario activo de una pantalla y su presentación. Antes de reemplazarlo o cerrarlo pregunta
/// al formulario si se puede salir (cambios sin guardar).
/// </summary>
public sealed partial class FormHost : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsSidePanel), nameof(IsFullScreen), nameof(IsListVisible))]
    public partial FormViewModel? ActiveForm { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidePanel), nameof(IsFullScreen), nameof(IsListVisible))]
    public partial FormPresentation ActivePresentation { get; private set; }

    public bool IsOpen => ActiveForm is not null;

    public bool IsSidePanel => IsOpen && ActivePresentation == FormPresentation.SidePanel;

    public bool IsFullScreen => IsOpen && ActivePresentation == FormPresentation.FullScreen;

    /// <summary>El listado se oculta solo con un formulario a pantalla completa.</summary>
    public bool IsListVisible => !IsFullScreen;

    /// <summary>Abre un formulario; si hay otro abierto, primero confirma salir de él.</summary>
    public async Task<bool> OpenAsync(FormViewModel form, FormPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (!await CloseActiveAsync())
        {
            return false;
        }

        form.Completed += OnFormCompleted;
        ActivePresentation = presentation;
        ActiveForm = form;
        return true;
    }

    /// <summary>Cierra el formulario activo si se puede salir de él.</summary>
    public async Task<bool> CloseActiveAsync()
    {
        if (ActiveForm is not { } form)
        {
            return true;
        }

        if (!await form.ConfirmLeaveAsync())
        {
            return false;
        }

        Detach(form);
        return true;
    }

    private void OnFormCompleted(object? sender, EventArgs e)
    {
        if (sender is FormViewModel form && ReferenceEquals(form, ActiveForm))
        {
            Detach(form);
        }
    }

    private void Detach(FormViewModel form)
    {
        form.Completed -= OnFormCompleted;
        ActiveForm = null;
    }
}
