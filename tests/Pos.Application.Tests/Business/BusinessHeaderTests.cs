using Pos.Application.Business;

namespace Pos.Application.Tests.Business;

/// <summary>Encabezado canónico del negocio para reportes y tickets (023, FR-023 a FR-025).</summary>
public sealed class BusinessHeaderTests
{
    private static BusinessProfileDto Profile(string address = "Calle 1 #23", string phone = "555 123 4567", string? taxId = "XAXX010101000") =>
        new("Mi Tienda", address, phone, taxId, "Gracias", [1, 2, 3]);

    [Fact]
    public void SinPerfil_EsNulo() => Assert.Null(BusinessHeader.From(null));

    [Fact]
    public void PerfilCompleto_LineasEnOrdenCanonico()
    {
        var header = BusinessHeader.From(Profile())!;

        Assert.Equal(
            [
                new BusinessHeaderLine("Mi Tienda", true),
                new BusinessHeaderLine("Calle 1 #23", false),
                new BusinessHeaderLine("Tel. 555 123 4567", false),
                new BusinessHeaderLine("RFC: XAXX010101000", false),
            ],
            header.Lines);
        Assert.Equal([1, 2, 3], header.Logo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinRfc_SeOmiteSinLineaVacia(string? taxId)
    {
        var header = BusinessHeader.From(Profile(taxId: taxId))!;

        Assert.Null(header.TaxId);
        Assert.Equal(["Mi Tienda", "Calle 1 #23", "Tel. 555 123 4567"], header.Lines.Select(l => l.Text));
    }

    [Fact]
    public void DireccionYTelefonoVacios_SeOmiten()
    {
        var header = BusinessHeader.From(Profile(address: " ", phone: ""))!;

        Assert.Equal(["Mi Tienda", "RFC: XAXX010101000"], header.Lines.Select(l => l.Text));
    }

    [Fact]
    public void LosTextosSeRecortan()
    {
        var header = BusinessHeader.From(new BusinessProfileDto("  Mi Tienda ", " Calle 1 ", " 555 ", " RFC1 ", null, null))!;

        Assert.Equal(["Mi Tienda", "Calle 1", "Tel. 555", "RFC: RFC1"], header.Lines.Select(l => l.Text));
    }

    [Fact]
    public void AvisoSinDatos() => Assert.Equal("Datos del negocio no capturados", BusinessHeader.MissingText);
}
