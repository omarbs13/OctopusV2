using Pos.Domain.Common;

namespace Pos.Domain.Tests.Common;

public class MoneyTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("89.5", 8950)]
    [InlineData("89.50", 8950)]
    [InlineData("12.30", 1230)]
    [InlineData("  12.30 ", 1230)]
    [InlineData("1234.05", 123405)]
    [InlineData("999999.99", 99_999_999)]
    [InlineData("007", 700)]
    public void TryParse_TextoValido_DevuelveCentavosExactos(string text, long expectedCents)
    {
        var ok = Money.TryParse(text, out var money);

        Assert.True(ok);
        Assert.Equal(expectedCents, money.Cents);
    }

    [Theory]
    [InlineData("1,234.50")]
    [InlineData("12,50")]
    [InlineData("12.345")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("$10")]
    [InlineData("1e3")]
    [InlineData("1000000")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12. 5")]
    [InlineData("12.")]
    [InlineData(".5")]
    [InlineData("abc")]
    [InlineData("١٢٣")]
    [InlineData(null)]
    public void TryParse_TextoInvalido_SeRechazaSinRedondear(string? text)
    {
        var ok = Money.TryParse(text, out var money);

        Assert.False(ok);
        Assert.Equal(Money.Zero, money);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(Money.MaxCents)]
    public void FromCents_DentroDeRango_Crea(long cents)
    {
        Assert.Equal(cents, Money.FromCents(cents).Cents);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Money.MaxCents + 1)]
    public void FromCents_FueraDeRango_Lanza(long cents)
    {
        Assert.Throws<DomainException>(() => Money.FromCents(cents));
    }

    [Fact]
    public void Igualdad_EsPorValor()
    {
        Assert.Equal(Money.FromCents(8950), Money.FromCents(8950));
        Assert.NotEqual(Money.FromCents(8950), Money.FromCents(8951));
    }

    [Fact]
    public void ToEditableString_UsaPuntoYDosDecimalesSinSeparadorDeMiles()
    {
        Assert.Equal("1234.50", Money.FromCents(123450).ToEditableString());
        Assert.Equal("0.05", Money.FromCents(5).ToEditableString());
    }
}
