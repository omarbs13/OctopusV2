using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class ProductTests
{
    private static readonly Money Price = Money.FromCents(8950);

    [Fact]
    public void Create_DatosValidos_CreaProductoActivoVersionUnoConIdV7()
    {
        var product = Product.Create("  Café Molido ", "caf-001", "7501234567890", Price);

        Assert.Equal("Café Molido", product.Name);
        Assert.Equal("cafe molido", product.NameSearch);
        Assert.Equal("CAF-001", product.Sku);
        Assert.Equal("7501234567890", product.Barcode);
        Assert.Equal(Price, product.Price);
        Assert.True(product.IsActive);
        Assert.Equal(1, product.Version);
        Assert.Null(product.DeletedAt);
        Assert.False(product.IsDeleted);
        Assert.Equal(7, product.Id.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NombreVacio_Lanza(string name)
    {
        Assert.Throws<DomainException>(() => Product.Create(name, "SKU1", null, Price));
    }

    [Fact]
    public void Create_Nombre200Caracteres_EsValido_Y201Lanza()
    {
        Product.Create(new string('a', 200), "SKU1", null, Price);

        Assert.Throws<DomainException>(() => Product.Create(new string('a', 201), "SKU1", null, Price));
    }

    [Fact]
    public void Create_NombreConEspaciosExtremos_SeRecortaAntesDeMedir()
    {
        var product = Product.Create("  " + new string('a', 200) + "  ", "SKU1", null, Price);

        Assert.Equal(200, product.Name.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AB C")]
    [InlineData("AB\tC")]
    public void Create_SkuVacioOConEspacios_Lanza(string sku)
    {
        Assert.Throws<DomainException>(() => Product.Create("Nombre", sku, null, Price));
    }

    [Fact]
    public void Create_Sku50Caracteres_EsValido_Y51Lanza()
    {
        Product.Create("Nombre", new string('A', 50), null, Price);

        Assert.Throws<DomainException>(() => Product.Create("Nombre", new string('A', 51), null, Price));
    }

    [Fact]
    public void Create_SkuEnMinusculas_SeGuardaEnMayusculas()
    {
        Assert.Equal("ABC-1", Product.Create("Nombre", "abc-1", null, Price).Sku);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("12345678901234")]
    public void Create_CodigoDeBarrasDe8a14Digitos_EsValido(string barcode)
    {
        Assert.Equal(barcode, Product.Create("Nombre", "SKU1", barcode, Price).Barcode);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    [InlineData("12345678A")]
    [InlineData("1234 5678")]
    [InlineData("١٢٣٤٥٦٧٨")]
    public void Create_CodigoDeBarrasInvalido_Lanza(string barcode)
    {
        Assert.Throws<DomainException>(() => Product.Create("Nombre", "SKU1", barcode, Price));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_CodigoDeBarrasVacio_QuedaNulo(string? barcode)
    {
        Assert.Null(Product.Create("Nombre", "SKU1", barcode, Price).Barcode);
    }

    [Fact]
    public void Update_CambiaDatosYRecalculaNombreDeBusqueda()
    {
        var product = Product.Create("Café", "SKU1", null, Price);

        product.Update("Té Verde", "sku2", "12345678", Money.FromCents(100), isActive: false);

        Assert.Equal("Té Verde", product.Name);
        Assert.Equal("te verde", product.NameSearch);
        Assert.Equal("SKU2", product.Sku);
        Assert.Equal("12345678", product.Barcode);
        Assert.Equal(Money.FromCents(100), product.Price);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Update_DatosInvalidos_LanzaYNoModifica()
    {
        var product = Product.Create("Café", "SKU1", null, Price);

        Assert.Throws<DomainException>(() => product.Update("", "SKU1", null, Price, isActive: true));
        Assert.Equal("Café", product.Name);
    }

    [Fact]
    public void Delete_AsignaFechaDeBorradoYEsIdempotente()
    {
        var product = Product.Create("Café", "SKU1", null, Price);
        var first = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        product.Delete(first);
        product.Delete(first.AddHours(1));

        Assert.True(product.IsDeleted);
        Assert.Equal(first, product.DeletedAt);
    }

    [Fact]
    public void Delete_FechaNoUtc_Lanza()
    {
        var product = Product.Create("Café", "SKU1", null, Price);

        Assert.Throws<DomainException>(() => product.Delete(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local)));
    }
}
