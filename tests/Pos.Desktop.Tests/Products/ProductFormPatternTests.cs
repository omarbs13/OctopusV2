using Pos.Application.Products;
using Pos.Desktop.Forms;
using Pos.Desktop.Products;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

/// <summary>FR-025 a FR-027: Productos usa el formulario corto, con campos obligatorios marcados.</summary>
public sealed class ProductFormPatternTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ProductsViewModel CreatePage() =>
        new(_host.UseCases, _host.Runner, _host.Dialogs, () => new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs));

    [Fact]
    public async Task AltaYEdicion_SeAbrenEnPanelLateral()
    {
        _host.Repository.Seed(Product.Create("Café", "CAF-1", null, Money.FromCents(100), "H87"));
        var page = CreatePage();
        await page.OnActivatedAsync();

        await page.NewProductCommand.ExecuteAsync(null);
        Assert.Equal(FormPresentation.SidePanel, page.Forms.ActivePresentation);
        Assert.True(page.Forms.IsListVisible);
        await page.Forms.CloseActiveAsync();

        page.SelectedItem = page.Items.Single();
        await page.EditCommand.ExecuteAsync(null);
        Assert.Equal(FormPresentation.SidePanel, page.Forms.ActivePresentation);
        Assert.IsType<ProductEditorViewModel>(page.Forms.ActiveForm);
    }

    [Fact]
    public void CamposObligatorios_NombreSkuYPrecio()
    {
        var editor = new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs);

        Assert.True(editor.IsNameRequired);
        Assert.True(editor.IsSkuRequired);
        Assert.True(editor.IsPriceRequired);
        Assert.False(editor.IsBarcodeRequired);
    }

    [Fact]
    public async Task GuardarVacio_MuestraTodosLosErroresYEnfocaElPrimero()
    {
        var editor = new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(editor.NameError);
        Assert.NotNull(editor.SkuError);
        Assert.NotNull(editor.PriceError);
        Assert.Equal(ProductFields.Name, editor.FocusField);
    }

    [Fact]
    public void EditorNuevo_SinCambios_YConCambiosQueSeGuardarianIgual()
    {
        var editor = new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs);
        Assert.False(editor.IsDirty);

        editor.Name = "  ";
        editor.PriceText = " ";
        Assert.False(editor.IsDirty);

        editor.Name = "Café";
        Assert.True(editor.IsDirty);
    }
}
