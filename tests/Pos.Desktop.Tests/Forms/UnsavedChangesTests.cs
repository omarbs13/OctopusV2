using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Desktop.Tests.Home;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Forms;

/// <summary>
/// SC-005: confirmación de cambios sin guardar al cancelar, navegar, abrir otro registro y cerrar
/// la aplicación; sin pregunta si no hubo cambios o si se revirtieron.
/// </summary>
public sealed class UnsavedChangesTests : IDisposable
{
    private readonly DesktopTestHost _host = HomeTestSupport.CreateHostWithModules(
        s => s.AddSingleton<IPreferencesStore>(new InMemoryPreferencesStore()));

    public void Dispose() => _host.Dispose();

    private FakeDialogService Dialogs => _host.Dialogs;

    private Navigator Navigator => _host.Get<Navigator>();

    private async Task<(ProductsViewModel Page, ProductEditorViewModel Editor)> OpenNewProductWithChangesAsync()
    {
        await Navigator.NavigateAsync("catalogs.products");
        var page = (ProductsViewModel)Navigator.CurrentPage!;
        await page.NewProductCommand.ExecuteAsync(null);
        var editor = page.Editor!;
        editor.Name = "Café";
        editor.Sku = "caf-1";
        editor.PriceText = "10";
        return (page, editor);
    }

    // ---------- Cancelar ----------

    [Fact]
    public async Task Cancelar_ConCambios_SeguirEditando_NoCierra()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.KeepEditing;

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, Dialogs.UnsavedQuestions);
        Assert.Same(editor, page.Editor);
        Assert.Equal("Café", editor.Name);
    }

    [Fact]
    public async Task Cancelar_ConCambios_Descartar_CierraSinGuardar()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Null(page.Editor);
        Assert.Empty(_host.Repository.All);
    }

    [Fact]
    public async Task Cancelar_ConCambios_Guardar_GuardaYCierra()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Null(page.Editor);
        Assert.Single(_host.Repository.All);
    }

    [Fact]
    public async Task Cancelar_Guardar_ConErrores_SigueAbiertoConLosErrores()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        editor.PriceText = "12,50";
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Same(editor, page.Editor);
        Assert.NotNull(editor.PriceError);
        Assert.Empty(_host.Repository.All);
    }

    [Fact]
    public async Task Cancelar_Guardar_RechazadoPorDuplicado_SigueAbiertoConElError()
    {
        _host.Repository.Seed(Product.Create("Otro", "CAF-1", null, Money.FromCents(100), "H87"));
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Same(editor, page.Editor);
        Assert.Equal(Strings.Editor_DuplicateSku, editor.SkuError);
    }

    [Fact]
    public async Task Cancelar_Guardar_ConErrorInesperado_SigueAbiertoConLoCapturado()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        _host.Repository.FailWith = new InvalidOperationException("falla");

        await editor.CancelCommand.ExecuteAsync(null);

        Assert.Same(editor, page.Editor);
        Assert.Equal("Café", editor.Name);
        Assert.Contains(Dialogs.Messages, m => m.Message == Strings.Common_UnexpectedError);
    }

    // ---------- Navegar con el menú ----------

    [Fact]
    public async Task Navegar_ConCambios_SeguirEditando_NoCambiaDePantalla()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.KeepEditing;

        Assert.False(await Navigator.NavigateAsync("home"));

        Assert.Equal("catalogs.products", Navigator.CurrentEntryId);
        Assert.Same(editor, page.Editor);
    }

    [Fact]
    public async Task Navegar_ConCambios_Descartar_Navega()
    {
        var (page, _) = await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        Assert.True(await Navigator.NavigateAsync("home"));

        Assert.Equal("home", Navigator.CurrentEntryId);
        Assert.Null(page.Editor);
    }

    [Fact]
    public async Task Navegar_ConCambios_Guardar_GuardaYNavega()
    {
        await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.True(await Navigator.NavigateAsync("home"));

        Assert.Single(_host.Repository.All);
    }

    [Fact]
    public async Task NavegarALaOpcionYaAbierta_NoPreguntaNiReinicia()
    {
        var (page, editor) = await OpenNewProductWithChangesAsync();

        await Navigator.NavigateAsync("catalogs.products");

        Assert.Equal(0, Dialogs.UnsavedQuestions);
        Assert.Same(editor, page.Editor);
    }

    // ---------- Abrir otro registro ----------

    [Fact]
    public async Task AbrirOtroProducto_ConCambios_Pregunta()
    {
        _host.Repository.Seed(Product.Create("Leche", "LEC-1", null, Money.FromCents(100), "H87"));
        var (page, editor) = await OpenNewProductWithChangesAsync();
        page.SelectedItem = page.Items.Single();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.KeepEditing;

        await page.EditCommand.ExecuteAsync(null);

        Assert.Equal(1, Dialogs.UnsavedQuestions);
        Assert.Same(editor, page.Editor);
    }

    // ---------- Cerrar la aplicación ----------

    [Fact]
    public async Task CerrarLaAplicacion_ConCambios_SeguirEditando_NoCierra()
    {
        await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.KeepEditing;
        var main = new MainViewModel(Navigator, _host.Get<NavigationRegistry>(), _host.Get<MenuViewModel>(), _host.Get<IAppInfo>());

        Assert.False(await main.CanCloseAsync());
        Assert.Equal(1, Dialogs.UnsavedQuestions);
    }

    [Fact]
    public async Task CerrarLaAplicacion_ConCambios_Descartar_Cierra()
    {
        await OpenNewProductWithChangesAsync();
        Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;
        var main = new MainViewModel(Navigator, _host.Get<NavigationRegistry>(), _host.Get<MenuViewModel>(), _host.Get<IAppInfo>());

        Assert.True(await main.CanCloseAsync());
    }

    // ---------- Sin cambios ----------

    [Fact]
    public async Task SinCambiosOConCambiosRevertidos_NingunDisparadorPregunta()
    {
        _host.Repository.Seed(Product.Create("Leche", "LEC-1", null, Money.FromCents(1000), "H87"));
        await Navigator.NavigateAsync("catalogs.products");
        var page = (ProductsViewModel)Navigator.CurrentPage!;
        page.SelectedItem = page.Items.Single();
        await page.EditCommand.ExecuteAsync(null);
        var editor = page.Editor!;

        editor.Name = "Leche entera";
        editor.Name = "Leche";
        editor.Sku = "lec-1";
        editor.PriceText = "10";

        Assert.False(editor.IsDirty);
        Assert.True(await Navigator.NavigateAsync("home"));
        Assert.Equal(0, Dialogs.UnsavedQuestions);

        await Navigator.NavigateAsync("catalogs.products");
        await page.NewProductCommand.ExecuteAsync(null);
        await page.Editor!.CancelCommand.ExecuteAsync(null);
        Assert.Equal(0, Dialogs.UnsavedQuestions);
        Assert.Null(page.Editor);
    }

    // ---------- Formulario grande ----------

    [Fact]
    public async Task FormularioGrande_RegresarAlListadoConCambios_Pregunta()
    {
        var page = new TestFormsPageViewModel();
        var form = new SampleLargeFormViewModel(Dialogs);
        await page.Forms.OpenAsync(form, FormPresentation.FullScreen);
        form.Set(3, "algo");
        Dialogs.UnsavedChoice = UnsavedChangesChoice.KeepEditing;

        await form.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, Dialogs.UnsavedQuestions);
        Assert.Same(form, page.Forms.ActiveForm);
        Assert.False(await page.CanLeaveAsync());
    }
}
