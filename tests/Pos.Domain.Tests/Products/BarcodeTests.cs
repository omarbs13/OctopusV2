using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class BarcodeTests
{
    [Theory]
    [InlineData("PROD-0042", true)]
    [InlineData("ABC_123", false)]
    public void IsValidForCatalog_JuegoDeCaracteres(string normalized, bool expected)
    {
        Assert.Equal(expected, Barcode.IsValidForCatalog(normalized));
    }

    [Fact]
    public void IsValidForCatalog_48CaracteresValido_Y49Invalido()
    {
        Assert.True(Barcode.IsValidForCatalog(new string('A', 48)));
        Assert.False(Barcode.IsValidForCatalog(new string('A', 49)));
    }

    [Fact]
    public void IsValidForCatalog_Nulo_EsValido()
    {
        Assert.True(Barcode.IsValidForCatalog(null));
    }

    [Theory]
    [InlineData(" *abc-1* ", "ABC-1")]
    [InlineData("   ", null)]
    [InlineData("*", "*")]
    public void Normalize_RecortaQuitaAsteriscosYPasaAMayusculas(string raw, string? expected)
    {
        Assert.Equal(expected, Barcode.Normalize(raw));
    }

    [Theory]
    [InlineData("7501055300846", true)]
    [InlineData("7501055300847", false)]
    [InlineData("96385074", true)]
    [InlineData("96385075", false)]
    public void HasValidEanCheckDigit_Ean13YEan8(string digits, bool expected)
    {
        Assert.Equal(expected, Barcode.HasValidEanCheckDigit(digits));
    }

    [Theory]
    [InlineData("  ", BarcodeFormat.Empty)]
    [InlineData("7501055300846", BarcodeFormat.Ean13)]
    [InlineData("96385074", BarcodeFormat.Ean8)]
    [InlineData("*abc-12345*", BarcodeFormat.Code128OrCode39)]
    [InlineData("7501055300847", BarcodeFormat.Unrecognized)]
    [InlineData("ABC'123", BarcodeFormat.Unrecognized)]
    public void Classify_CadaFormato(string raw, BarcodeFormat expected)
    {
        Assert.Equal(expected, Barcode.Classify(raw));
    }
}
