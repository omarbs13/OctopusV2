using Pos.Desktop.Forms;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Forms;

/// <summary>SC-004: el patrón de formulario grande (pantalla completa) y su independencia de la presentación.</summary>
public class LargeFormTests
{
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task FormularioGrande_SeAbreAPantallaCompletaYRegresaAlListado()
    {
        var page = new TestFormsPageViewModel();
        var form = new SampleLargeFormViewModel(_dialogs);

        Assert.True(await page.Forms.OpenAsync(form, FormPresentation.FullScreen));

        Assert.Same(form, page.Forms.ActiveForm);
        Assert.True(page.Forms.IsFullScreen);
        Assert.False(page.Forms.IsSidePanel);
        Assert.False(page.Forms.IsListVisible);

        await form.CancelCommand.ExecuteAsync(null);

        Assert.Null(page.Forms.ActiveForm);
        Assert.True(page.Forms.IsListVisible);
    }

    [Fact]
    public async Task FormularioGrande_ValidaAlGuardarYEnfocaElPrimerError()
    {
        var page = new TestFormsPageViewModel();
        var form = new SampleLargeFormViewModel(_dialogs);
        await page.Forms.OpenAsync(form, FormPresentation.FullScreen);
        form.Set(0, "Uno");

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal([1, 6, 7], form.Errors.Keys.Order());
        Assert.Equal("Field1", form.FocusField);
        Assert.Same(form, page.Forms.ActiveForm);
    }

    [Theory]
    [InlineData(FormPresentation.FullScreen)]
    [InlineData(FormPresentation.SidePanel)]
    public async Task MismoFormulario_SeComportaIgualEnAmbasPresentaciones(FormPresentation presentation)
    {
        var page = new TestFormsPageViewModel();
        var form = new SampleLargeFormViewModel(_dialogs);
        string? saved = null;
        form.Saved += (_, v) => saved = v;
        await page.Forms.OpenAsync(form, presentation);

        foreach (var index in SampleLargeFormViewModel.RequiredFields)
        {
            form.Set(index, "valor");
        }

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("guardado", saved);
        Assert.Null(page.Forms.ActiveForm);
        Assert.True(page.Forms.IsListVisible);
    }
}
