using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class UnitOfMeasureTests
{
    [Fact]
    public void Catalogo_TieneOchoUnidadesConClavesUnicasYPiezaPorDefecto()
    {
        Assert.Equal(
            ["H87", "KGM", "GRM", "LTR", "MLT", "MTR", "XBX", "XPK"],
            UnitOfMeasure.All.OrderBy(u => u.SortOrder).Select(u => u.Code));
        Assert.Equal(8, UnitOfMeasure.All.Select(u => u.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("H87", UnitOfMeasure.Default.Code);
        Assert.Equal("Pieza", UnitOfMeasure.Default.Name);
        Assert.All(UnitOfMeasure.All, u => Assert.True(u.Code.Length <= UnitOfMeasure.CodeMaxLength));
    }

    [Theory]
    [InlineData("H87", true)]
    [InlineData("XPK", true)]
    [InlineData("h87", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("KG", false)]
    public void IsValidCode_ComparacionExacta(string? code, bool expected)
    {
        Assert.Equal(expected, UnitOfMeasure.IsValidCode(code));
    }
}
