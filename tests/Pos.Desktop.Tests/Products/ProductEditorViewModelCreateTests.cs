using Pos.Application.Products;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

public sealed class ProductEditorViewModelCreateTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ProductEditorViewModel CreateEditor() => new(_host.UseCases, _host.Runner, _host.Dialogs);

    private static void Fill(ProductEditorViewModel editor, string name = "Café Molido", string sku = "caf-001", string barcode = "", string price = "89.5")
    {
        editor.Name = name;
        editor.Sku = sku;
        editor.Barcode = barcode;
        editor.PriceText = price;
    }

    [Fact]
    public async Task EditorNuevo_EsModoAltaConProductoActivo()
    {
        var editor = CreateEditor();

        Assert.False(editor.IsEditMode);
        Assert.True(editor.IsActive);
        Assert.Equal(Strings.Editor_NewTitle, editor.Title);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GuardarValido_RegistraUnaVezYAvisaQueSeGuardo()
    {
        var editor = CreateEditor();
        ProductDto? saved = null;
        editor.Saved += (_, dto) => saved = dto;
        Fill(editor);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(saved);
        Assert.Equal("CAF-001", saved.Sku);
        Assert.Equal(1, _host.Repository.SaveCount);
    }

    [Fact]
    public async Task DobleClicEnGuardar_RegistraUnSoloProducto()
    {
        var editor = CreateEditor();
        Fill(editor);
        var gate = new TaskCompletionSource();
        _host.Repository.SaveGate = gate;

        // Como lo haría el botón: solo ejecuta si el comando lo permite.
        ClickSave(editor);
        ClickSave(editor);
        Assert.False(editor.SaveCommand.CanExecute(null));

        gate.SetResult();
        await WaitUntilAsync(() => !editor.SaveCommand.IsRunning);

        Assert.Equal(1, _host.Repository.AddCount);
        Assert.Single(_host.Repository.All);
    }

    [Fact]
    public async Task DatosInvalidos_MuestraErroresPorCampoYConservaLoCapturado()
    {
        var editor = CreateEditor();
        Fill(editor, name: "", sku: "A B", barcode: "12", price: "12,50");
        var savedRaised = false;
        editor.Saved += (_, _) => savedRaised = true;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.False(savedRaised);
        Assert.Equal(ProductMessages.NameRequired, editor.NameError);
        Assert.Equal(ProductMessages.SkuWithSpaces, editor.SkuError);
        Assert.Equal(ProductMessages.BarcodeFormat, editor.BarcodeError);
        Assert.Equal(ProductMessages.PriceFormat, editor.PriceError);
        Assert.Equal(ProductFields.Name, editor.FocusField);
        Assert.Equal(("", "A B", "12", "12,50"), (editor.Name, editor.Sku, editor.Barcode, editor.PriceText));
    }

    [Fact]
    public async Task CorregirYGuardar_LimpiaLosErroresAnteriores()
    {
        var editor = CreateEditor();
        Fill(editor, name: "");
        await editor.SaveCommand.ExecuteAsync(null);

        editor.Name = "Café";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Null(editor.NameError);
        Assert.Single(_host.Repository.All);
    }

    [Fact]
    public async Task SkuDuplicado_MuestraElMensajeEnElCampoSku()
    {
        _host.Repository.Seed(Product.Create("Otro", "CAF-001", null, Money.FromCents(100), "H87"));
        var editor = CreateEditor();
        Fill(editor);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Editor_DuplicateSku, editor.SkuError);
        Assert.Equal(ProductFields.Sku, editor.FocusField);
    }

    [Fact]
    public async Task CodigoDeBarrasDuplicado_MuestraElMensajeEnSuCampo()
    {
        _host.Repository.Seed(Product.Create("Otro", "OTRO-1", "7501234567890", Money.FromCents(100), "H87"));
        var editor = CreateEditor();
        Fill(editor, barcode: "7501234567890");

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Editor_DuplicateBarcode, editor.BarcodeError);
    }

    [Fact]
    public void Cancelar_AvisaQueSeCerroSinGuardar()
    {
        var editor = CreateEditor();
        var closed = false;
        editor.Closed += (_, _) => closed = true;

        editor.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Empty(_host.Repository.All);
    }

    private static void ClickSave(ProductEditorViewModel editor)
    {
        if (editor.SaveCommand.CanExecute(null))
        {
            editor.SaveCommand.Execute(null);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }
}
