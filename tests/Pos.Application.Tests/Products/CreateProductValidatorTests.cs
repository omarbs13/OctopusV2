using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;

namespace Pos.Application.Tests.Products;

public class CreateProductValidatorTests
{
    private static readonly CreateProductValidator Validator = new();

    private static CreateProductCommand Valid() => new("Café Molido", "CAF-001", "7501234567890", "89.50");

    private static IEnumerable<(string Field, string Message)> Errors(CreateProductCommand command) =>
        Validator.Validate(command).Errors.Select(e => (e.PropertyName, e.ErrorMessage));

    [Fact]
    public void ComandoValido_NoTieneErrores()
    {
        Assert.Empty(Errors(Valid()));
        Assert.Empty(Errors(Valid() with { Barcode = null }));
        Assert.Empty(Errors(Valid() with { Barcode = "  " }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NombreVacio_EsObligatorio(string name)
    {
        Assert.Equal([(ProductFields.Name, ProductMessages.NameRequired)], Errors(Valid() with { Name = name }));
    }

    [Fact]
    public void Nombre201Caracteres_ExcedeElMaximo()
    {
        Assert.Equal(
            [(ProductFields.Name, ProductMessages.NameTooLong)],
            Errors(Valid() with { Name = new string('a', 201) }));
        Assert.Empty(Errors(Valid() with { Name = "  " + new string('a', 200) + "  " }));
    }

    [Fact]
    public void SkuVacio_EsObligatorio()
    {
        Assert.Equal([(ProductFields.Sku, ProductMessages.SkuRequired)], Errors(Valid() with { Sku = "" }));
    }

    [Fact]
    public void Sku51Caracteres_ExcedeElMaximo()
    {
        Assert.Equal([(ProductFields.Sku, ProductMessages.SkuTooLong)], Errors(Valid() with { Sku = new string('A', 51) }));
    }

    [Fact]
    public void SkuConEspacios_SeRechaza()
    {
        Assert.Equal([(ProductFields.Sku, ProductMessages.SkuWithSpaces)], Errors(Valid() with { Sku = "CAF 001" }));
    }

    [Theory]
    [InlineData("75012345678AB")]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    public void CodigoDeBarrasInvalido_SeRechaza(string barcode)
    {
        Assert.Equal(
            [(ProductFields.Barcode, ProductMessages.BarcodeFormat)],
            Errors(Valid() with { Barcode = barcode }));
    }

    [Theory]
    [InlineData("1,234.50")]
    [InlineData("12.345")]
    [InlineData("-1")]
    [InlineData("1000000")]
    [InlineData("$10")]
    public void PrecioConFormatoInvalido_SeRechazaSinRedondear(string price)
    {
        Assert.Equal([(ProductFields.Price, ProductMessages.PriceFormat)], Errors(Valid() with { PriceText = price }));
    }

    [Fact]
    public void PrecioVacio_EsObligatorio()
    {
        Assert.Equal([(ProductFields.Price, ProductMessages.PriceRequired)], Errors(Valid() with { PriceText = " " }));
    }

    [Fact]
    public void VariosCamposInvalidos_ReportaUnErrorPorCampo()
    {
        var errors = Errors(new CreateProductCommand("", "", "12", "abc")).Select(e => e.Field);

        Assert.Equal([ProductFields.Name, ProductFields.Sku, ProductFields.Barcode, ProductFields.Price], errors);
    }
}
