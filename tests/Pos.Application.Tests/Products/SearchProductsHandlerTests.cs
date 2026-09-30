using Pos.Application.Products;
using Pos.Application.Products.SearchProducts;
using Pos.Application.Tests.TestSupport;

namespace Pos.Application.Tests.Products;

public class SearchProductsHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();

    private SearchProductsHandler Handler => new(_repository);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Texto_SeNormalizaParaNombreYSku()
    {
        await Handler.HandleAsync(new SearchProductsQuery("  Café Molido ", IncludeInactive: false), Ct);

        Assert.Equal(
            new ProductSearch("cafe molido", "CAFÉ MOLIDO", "Café Molido", BarcodeExact: false, IncludeInactive: false, Page: 1, ProductPage.DefaultPageSize),
            _repository.LastSearch);
    }

    [Theory]
    [InlineData("75012345", true)]
    [InlineData("7501234567890", true)]
    [InlineData("75012345678901", true)]
    [InlineData("7501234", false)]
    [InlineData("750123456789012", false)]
    [InlineData("7501234A", false)]
    public async Task CodigoDeBarrasCompleto_SeBuscaExacto(string text, bool exact)
    {
        await Handler.HandleAsync(new SearchProductsQuery(text, IncludeInactive: false), Ct);

        Assert.Equal(exact, _repository.LastSearch!.BarcodeExact);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TextoVacio_DevuelveTodosLosVisibles(string? text)
    {
        await Handler.HandleAsync(new SearchProductsQuery(text, IncludeInactive: true), Ct);

        Assert.Equal(
            new ProductSearch(null, null, null, BarcodeExact: false, IncludeInactive: true, Page: 1, ProductPage.DefaultPageSize),
            _repository.LastSearch);
    }

    [Fact]
    public void Paginas_SonDe100Registros()
    {
        Assert.Equal(100, ProductPage.DefaultPageSize);
    }
}
