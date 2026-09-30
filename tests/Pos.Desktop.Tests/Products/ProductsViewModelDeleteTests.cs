using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

public sealed class ProductsViewModelDeleteTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<ProductsViewModel> CreatePageWithSelectionAsync()
    {
        _host.Repository.Seed(Product.Create("Café Molido", "CAF-001", null, Money.FromCents(100)));
        var page = new ProductsViewModel(_host.UseCases, _host.Runner, _host.Dialogs, () => new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs));
        await page.OnActivatedAsync();
        page.SelectedItem = page.Items.Single();
        return page;
    }

    [Fact]
    public async Task Borrar_PideConfirmacionConNombreYSku()
    {
        var page = await CreatePageWithSelectionAsync();
        _host.Dialogs.ConfirmResult = false;

        await page.DeleteCommand.ExecuteAsync(null);

        var message = Assert.Single(_host.Dialogs.Confirmations);
        Assert.Contains("«Café Molido»", message, StringComparison.Ordinal);
        Assert.Contains("(CAF-001)", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelar_NoBorra()
    {
        var page = await CreatePageWithSelectionAsync();
        _host.Dialogs.ConfirmResult = false;

        await page.DeleteCommand.ExecuteAsync(null);

        Assert.False(_host.Repository.All.Single().IsDeleted);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Confirmar_BorraYRefrescaLaLista()
    {
        var page = await CreatePageWithSelectionAsync();
        _host.Dialogs.ConfirmResult = true;

        await page.DeleteCommand.ExecuteAsync(null);

        Assert.True(_host.Repository.All.Single().IsDeleted);
        Assert.Empty(page.Items);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task Conflicto_MuestraMensajeYRefresca()
    {
        var page = await CreatePageWithSelectionAsync();
        _host.Repository.BumpVersion(page.SelectedItem!.Id);
        _host.Dialogs.ConfirmResult = true;

        await page.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Products_DeleteConflict, Assert.Single(_host.Dialogs.Messages).Message);
        Assert.False(_host.Repository.All.Single().IsDeleted);
        Assert.Equal(2, page.Items.Single().Version);
    }

    [Fact]
    public async Task YaNoExiste_MuestraMensajeYRefresca()
    {
        var page = await CreatePageWithSelectionAsync();
        _host.Repository.All.Single().Delete(DateTime.UtcNow);
        _host.Dialogs.ConfirmResult = true;

        await page.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Editor_NotFound, Assert.Single(_host.Dialogs.Messages).Message);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task SinSeleccion_BorrarNoEstaDisponible()
    {
        var page = await CreatePageWithSelectionAsync();
        page.SelectedItem = null;

        Assert.False(page.DeleteCommand.CanExecute(null));
    }
}
