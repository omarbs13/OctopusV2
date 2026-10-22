using Pos.Application.Products;
using Pos.Application.Products.UpdateProduct;

namespace Pos.Application.Tests.Products;

/// <summary>La edición aplica las mismas reglas que el alta (003, FR-020).</summary>
public class UpdateProductValidatorTests
{
    private static readonly UpdateProductValidator Validator = new();

    private static UpdateProductCommand Valid() =>
        new(Guid.CreateVersion7(), 1, "Café Molido", "CAF-001", null, "KGM", IsActive: true, PiecePriceText: "89.50");

    private static IEnumerable<(string Field, string Message)> Errors(UpdateProductCommand command) =>
        Validator.Validate(command).Errors.Select(e => (e.PropertyName, e.ErrorMessage));

    [Fact]
    public void DatosValidos_SinErrores()
    {
        Assert.Empty(Errors(Valid()));
    }

    [Theory]
    [InlineData(" ", ProductMessages.PriceRequired)]
    [InlineData("12,50", ProductMessages.PriceFormat)]
    [InlineData("0.001", ProductMessages.PriceTooManyDecimals)]
    [InlineData("1000000", ProductMessages.PriceTooLarge)]
    [InlineData("0", ProductMessages.PriceNotPositive)]
    public void PrecioInvalido_MismosMensajesQueElAlta(string price, string message)
    {
        Assert.Equal([(ProductFields.Price, message)], Errors(Valid() with { PiecePriceText = price }));
    }

    [Fact]
    public void UnidadInexistente_EsObligatoria()
    {
        Assert.Equal([(ProductFields.UnitCode, ProductMessages.UnitRequired)], Errors(Valid() with { UnitCode = "ZZZ" }));
    }

    [Fact]
    public void ObligatoriosVacios_CadaUnoEnSuCampo()
    {
        var fields = Errors(Valid() with { Model = "", Sku = "A B", PiecePriceText = "", UnitCode = "" }).Select(e => e.Field);

        Assert.Equal([ProductFields.Model, ProductFields.Sku, ProductFields.Price, ProductFields.UnitCode], fields);
    }
}
