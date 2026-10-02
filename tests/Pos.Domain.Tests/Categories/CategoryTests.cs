using Pos.Domain.Categories;
using Pos.Domain.Common;

namespace Pos.Domain.Tests.Categories;

/// <summary>016: validaciones de integridad de la categoría (FR-002, FR-003).</summary>
public sealed class CategoryTests
{
    [Fact]
    public void Crear_NombreRecortado_ClaveSinAcentosNiMayusculas_YDescripcionVaciaNula()
    {
        var category = Category.Create("  Bebidas ", "   ");

        Assert.Equal("Bebidas", category.Name);
        Assert.Null(category.Description);
        Assert.True(category.IsActive);
        Assert.Equal(1, category.Version);
        Assert.Equal("bebidas", Category.Create("BEBÍDAS", null).NameKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_NombreVacio_SeRechaza(string name) =>
        Assert.Throws<DomainException>(() => Category.Create(name, null));

    [Fact]
    public void Longitudes_LimiteAceptado_YExcedidoRechazado()
    {
        Assert.Equal(50, Category.Create(new string('a', 50), new string('d', 200)).Name.Length);
        Assert.Throws<DomainException>(() => Category.Create(new string('a', 51), null));
        Assert.Throws<DomainException>(() => Category.Create("Bebidas", new string('d', 201)));
    }
}
