using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class ProductTests
{
    private static readonly Money Price = Money.FromCents(8950);

    [Fact]
    public void Create_DatosValidos_CreaProductoActivoVersionUnoConIdV7()
    {
        var product = Product.Create("  Café Molido ", "caf-001", "7501234567890", Price, "H87");

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
        Assert.Throws<DomainException>(() => Product.Create(name, "SKU1", null, Price, "H87"));
    }

    [Fact]
    public void Create_Nombre200Caracteres_EsValido_Y201Lanza()
    {
        Product.Create(new string('a', 200), "SKU1", null, Price, "H87");

        Assert.Throws<DomainException>(() => Product.Create(new string('a', 201), "SKU1", null, Price, "H87"));
    }

    [Fact]
    public void Create_NombreConEspaciosExtremos_SeRecortaAntesDeMedir()
    {
        var product = Product.Create("  " + new string('a', 200) + "  ", "SKU1", null, Price, "H87");

        Assert.Equal(200, product.Name.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AB C")]
    [InlineData("AB\tC")]
    public void Create_SkuVacioOConEspacios_Lanza(string sku)
    {
        Assert.Throws<DomainException>(() => Product.Create("Nombre", sku, null, Price, "H87"));
    }

    [Fact]
    public void Create_Sku50Caracteres_EsValido_Y51Lanza()
    {
        Product.Create("Nombre", new string('A', 50), null, Price, "H87");

        Assert.Throws<DomainException>(() => Product.Create("Nombre", new string('A', 51), null, Price, "H87"));
    }

    [Fact]
    public void Create_SkuEnMinusculas_SeGuardaEnMayusculas()
    {
        Assert.Equal("ABC-1", Product.Create("Nombre", "abc-1", null, Price, "H87").Sku);
    }

    [Theory]
    [InlineData("12345678", "12345678")]
    [InlineData("12345678901234", "12345678901234")]
    [InlineData("PROD-0042", "PROD-0042")]
    [InlineData("abc-12345", "ABC-12345")]
    public void Create_CodigoDeBarrasNumericoOAlfanumerico_EsValido(string barcode, string expected)
    {
        Assert.Equal(expected, Product.Create("Nombre", "SKU1", barcode, Price, "H87").Barcode);
    }

    [Theory]
    [InlineData("١٢٣٤٥٦٧٨")]
    [InlineData("ABC_123")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Create_CodigoDeBarrasInvalido_Lanza(string barcode)
    {
        Assert.Throws<DomainException>(() => Product.Create("Nombre", "SKU1", barcode, Price, "H87"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_CodigoDeBarrasVacio_QuedaNulo(string? barcode)
    {
        Assert.Null(Product.Create("Nombre", "SKU1", barcode, Price, "H87").Barcode);
    }

    [Fact]
    public void Update_CambiaDatosYRecalculaNombreDeBusqueda()
    {
        var product = Product.Create("Café", "SKU1", null, Price, "H87");

        product.Update("Té Verde", "sku2", "12345678", Money.FromCents(100), "H87", isActive: false);

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
        var product = Product.Create("Café", "SKU1", null, Price, "H87");

        Assert.Throws<DomainException>(() => product.Update("", "SKU1", null, Price, "H87", isActive: true));
        Assert.Equal("Café", product.Name);
    }

    [Fact]
    public void Delete_AsignaFechaDeBorradoYEsIdempotente()
    {
        var product = Product.Create("Café", "SKU1", null, Price, "H87");
        var first = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        product.Delete(first);
        product.Delete(first.AddHours(1));

        Assert.True(product.IsDeleted);
        Assert.Equal(first, product.DeletedAt);
    }

    [Fact]
    public void Delete_FechaNoUtc_Lanza()
    {
        var product = Product.Create("Café", "SKU1", null, Price, "H87");

        Assert.Throws<DomainException>(() => product.Delete(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Local)));
    }
    [Fact]
    public void Create_PrecioCero_Lanza_YUnCentavoEsValido()
    {
        Assert.Throws<DomainException>(() => Product.Create("Café", "SKU1", null, Money.Zero, "H87"));

        Assert.Equal(1, Product.Create("Café", "SKU1", null, Money.FromCents(1), "H87").Price.Cents);
    }

    [Fact]
    public void Update_PrecioCero_Lanza()
    {
        var product = Product.Create("Café", "SKU1", null, Price, "H87");

        Assert.Throws<DomainException>(() => product.Update("Café", "SKU1", null, Money.Zero, "H87", isActive: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("h87")]
    public void UnidadInexistente_Lanza(string unitCode)
    {
        Assert.Throws<DomainException>(() => Product.Create("Café", "SKU1", null, Price, unitCode));

        var product = Product.Create("Café", "SKU1", null, Price, "H87");
        Assert.Throws<DomainException>(() => product.Update("Café", "SKU1", null, Price, unitCode, isActive: true));
    }

    [Fact]
    public void Update_CambiaLaUnidad()
    {
        var product = Product.Create("Café", "SKU1", null, Price, "H87");

        product.Update("Café", "SKU1", null, Price, "KGM", isActive: true);

        Assert.Equal("KGM", product.UnitCode);
    }

    [Fact]
    public void PuntoDeReorden_MenorQueElMinimo_EsValido()
    {
        var product = Product.Create(
            "Café", "SKU1", null, Price, "H87", tracksInventory: true,
            minimumStock: Quantity.FromThousandths(20_000), reorderPoint: Quantity.FromThousandths(5_000));

        Assert.Equal(5_000, product.ReorderPointThousandths);
        Assert.Equal(Quantity.FromThousandths(5_000), product.ReorderPoint);
    }

    [Fact]
    public void PuntoDeReorden_IgualAlMinimo_Lanza()
    {
        Assert.Throws<DomainException>(() => Product.Create(
            "Café", "SKU1", null, Price, "H87", tracksInventory: true,
            minimumStock: Quantity.FromThousandths(20_000), reorderPoint: Quantity.FromThousandths(20_000)));

        var product = Product.Create("Café", "SKU1", null, Price, "H87", tracksInventory: true);
        Assert.Throws<DomainException>(() => product.Update(
            "Café", "SKU1", null, Price, "H87", isActive: true, tracksInventory: true,
            minimumStock: Quantity.FromThousandths(20_000), reorderPoint: Quantity.FromThousandths(20_000)));
    }

    [Fact]
    public void PuntoDeReorden_ConDecimalesEnPiezas_Lanza()
    {
        Assert.False(Product.IsValidReorderPoint(Quantity.FromThousandths(2_500), null, UnitOfMeasure.Piece));
        Assert.Throws<DomainException>(() => Product.Create(
            "Café", "SKU1", null, Price, "H87", tracksInventory: true, reorderPoint: Quantity.FromThousandths(2_500)));
    }

    [Fact]
    public void PuntoDeReorden_Cero_EsValido()
    {
        Assert.True(Product.IsValidReorderPoint(Quantity.Zero, Quantity.FromThousandths(1_000), UnitOfMeasure.Piece));
    }

    [Fact]
    public void PuntoDeReorden_SinControlDeInventario_QuedaNulo()
    {
        var product = Product.Create(
            "Café", "SKU1", null, Price, "H87", tracksInventory: false,
            minimumStock: Quantity.FromThousandths(20_000), reorderPoint: Quantity.FromThousandths(20_000));

        Assert.Null(product.ReorderPointThousandths);
        Assert.Null(product.MinimumStockThousandths);
    }
}
