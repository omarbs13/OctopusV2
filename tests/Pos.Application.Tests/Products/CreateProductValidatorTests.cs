using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;

namespace Pos.Application.Tests.Products;

public class CreateProductValidatorTests
{
    private static readonly CreateProductValidator Validator = new();

    private static CreateProductCommand Valid() => new("Café Molido", "CAF-001", "7501234567890", "89.50", "H87");

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
    [InlineData("ABC_123")]
    [InlineData("ABC'123")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void CodigoDeBarrasInvalido_SeRechaza(string barcode)
    {
        Assert.Equal(
            [(ProductFields.Barcode, ProductMessages.BarcodeFormat)],
            Errors(Valid() with { Barcode = barcode }));
    }

    [Theory]
    [InlineData("12,50", ProductMessages.PriceFormat)]
    [InlineData("1,23.45", ProductMessages.PriceFormat)]
    [InlineData("-1", ProductMessages.PriceFormat)]
    [InlineData("$10", ProductMessages.PriceFormat)]
    [InlineData("12.345", ProductMessages.PriceTooManyDecimals)]
    [InlineData("999999.991", ProductMessages.PriceTooManyDecimals)]
    [InlineData("1000000", ProductMessages.PriceTooLarge)]
    [InlineData("1,000,000.00", ProductMessages.PriceTooLarge)]
    [InlineData("0", ProductMessages.PriceNotPositive)]
    [InlineData("0.00", ProductMessages.PriceNotPositive)]
    public void PrecioInvalido_MensajeEspecificoSinRedondear(string price, string message)
    {
        Assert.Equal([(ProductFields.Price, message)], Errors(Valid() with { PriceText = price }));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("999999.99")]
    [InlineData("999,999.99")]
    [InlineData("1,234.50")]
    [InlineData("1234.50")]
    public void PrecioEnLosLimites_EsValido(string price)
    {
        Assert.Empty(Errors(Valid() with { PriceText = price }));
    }

    [Fact]
    public void PrecioVacio_EsObligatorio()
    {
        Assert.Equal([(ProductFields.Price, ProductMessages.PriceRequired)], Errors(Valid() with { PriceText = " " }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("h87")]
    public void UnidadFaltanteOInexistente_EsObligatoria(string unitCode)
    {
        Assert.Equal([(ProductFields.UnitCode, ProductMessages.UnitRequired)], Errors(Valid() with { UnitCode = unitCode }));
    }

    [Fact]
    public void VariosCamposInvalidos_ReportaUnErrorPorCampo()
    {
        var errors = Errors(new CreateProductCommand("", "", "AB_12", "abc", "")).Select(e => e.Field);

        Assert.Equal([ProductFields.Name, ProductFields.Sku, ProductFields.Barcode, ProductFields.Price, ProductFields.UnitCode], errors);
    }
}
