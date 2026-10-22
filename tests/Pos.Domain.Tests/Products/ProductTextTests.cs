using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class ProductTextTests
{
    [Theory]
    [InlineData("33 X 33", "33X33")]
    [InlineData("33x33", "33X33")]
    [InlineData("120*240", "120X240")]
    [InlineData("120 × 240", "120X240")]
    [InlineData("1.22 X 2.44 X .03", "1.22X2.44X.03")]
    [InlineData("  60 x 60  ", "60X60")]
    public void NormalizeMeasures_QuitaEspaciosYUnificaLaPor(string input, string expected)
    {
        Assert.Equal(expected, ProductText.NormalizeMeasures(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeMeasures_VacioEsNulo(string? input)
    {
        Assert.Null(ProductText.NormalizeMeasures(input));
    }

    [Theory]
    [InlineData("Sunwood  Miel", "SUNWOOD MIEL")]
    [InlineData("  Cerámica Ñandú ", "CERAMICA NANDU")]
    [InlineData("A&B - 1/2 (Extra)", "A&B - 1/2 (EXTRA)")]
    public void NormalizeModel_MayusculasSinAcentosYEspaciosReducidos(string input, string expected)
    {
        Assert.Equal(expected, ProductText.NormalizeModel(input));
    }

    [Fact]
    public void BuildIdentityKey_VariantesDeLaMismaMedidaYModeloDanLaMismaClave()
    {
        var supplier = Guid.CreateVersion7();

        Assert.Equal(
            ProductText.BuildIdentityKey(supplier, "SUNWOOD MIEL", "18X60"),
            ProductText.BuildIdentityKey(supplier, "sunwood  miel", "18 x 60"));
        Assert.Equal(
            ProductText.BuildIdentityKey(supplier, "Piedra", "120X240"),
            ProductText.BuildIdentityKey(supplier, "Piedra", "120*240"));
    }

    [Fact]
    public void BuildIdentityKey_OtraMedidaOOtroProveedorEsOtraClave()
    {
        var supplier = Guid.CreateVersion7();
        var key = ProductText.BuildIdentityKey(supplier, "SUNWOOD MIEL", "18X60");

        Assert.NotEqual(key, ProductText.BuildIdentityKey(supplier, "SUNWOOD MIEL", "20X90"));
        Assert.NotEqual(key, ProductText.BuildIdentityKey(Guid.CreateVersion7(), "SUNWOOD MIEL", "18X60"));
        Assert.NotEqual(key, ProductText.BuildIdentityKey(null, "SUNWOOD MIEL", "18X60"));
    }

    [Fact]
    public void BuildIdentityKey_SinProveedorSoloChocaConSinProveedor()
    {
        Assert.Equal(
            ProductText.BuildIdentityKey(null, "Piedra", null),
            ProductText.BuildIdentityKey(null, "PIEDRA", " "));
    }

    [Fact]
    public void BuildName_ModeloYMedidas_TruncaA200()
    {
        Assert.Equal("Piedra 33 X 33", ProductText.BuildName("Piedra", "33 X 33"));
        Assert.Equal("Piedra", ProductText.BuildName("Piedra", null));
        Assert.Equal(Product.NameMaxLength, ProductText.BuildName(new string('a', 200), "33X33").Length);
    }

    [Fact]
    public void GenerateSku_EsValidoYDistinto()
    {
        var first = ProductText.GenerateSku();

        Assert.True(Product.IsValidSku(first));
        Assert.NotEqual(first, ProductText.GenerateSku());
    }
}
