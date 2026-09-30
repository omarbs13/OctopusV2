using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Forms;

public class FormViewModelTests
{
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public void Cambios_SeDetectanYSeRevierten()
    {
        var form = new MinimalFormViewModel(_dialogs);
        Assert.False(form.IsDirty);

        form.Name = "Café";
        Assert.True(form.IsDirty);

        form.Name = "";
        Assert.False(form.IsDirty);
    }

    [Fact]
    public void CambioQueSeGuardariaIgual_NoCuentaComoCambio()
    {
        var form = new MinimalFormViewModel(_dialogs) { Code = "ABC" };
        form.AcceptCurrentAsOriginal();

        form.Code = " abc ";

        Assert.False(form.IsDirty);
    }

    [Fact]
    public async Task DobleClicEnGuardar_UnSoloGuardado()
    {
        var form = new MinimalFormViewModel(_dialogs) { Name = "Café" };
        var gate = new TaskCompletionSource();
        form.SaveGate = gate;

        if (form.SaveCommand.CanExecute(null))
        {
            form.SaveCommand.Execute(null);
        }

        if (form.SaveCommand.CanExecute(null))
        {
            form.SaveCommand.Execute(null);
        }

        gate.SetResult();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, form.SaveAttempts);
    }

    [Fact]
    public async Task GuardadoExitoso_EmiteSavedYReiniciaElEstadoOriginal()
    {
        var form = new MinimalFormViewModel(_dialogs) { Name = "Café" };
        string? saved = null;
        form.Saved += (_, value) => saved = value;

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Café", saved);
        Assert.False(form.IsDirty);
    }

    [Fact]
    public async Task ErroresSoloAlGuardar_NoAlCambiarCampos()
    {
        var form = new MinimalFormViewModel(_dialogs) { Name = "x" };
        form.Name = "";
        Assert.Null(form.NameError);

        await form.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(form.NameError);
        Assert.Equal("Name", form.FocusField);
    }

    [Fact]
    public async Task CancelarSinCambios_CierraSinPreguntar()
    {
        var form = new MinimalFormViewModel(_dialogs);
        var closed = false;
        form.Closed += (_, _) => closed = true;

        await form.CancelCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Equal(0, _dialogs.UnsavedQuestions);
    }
}
