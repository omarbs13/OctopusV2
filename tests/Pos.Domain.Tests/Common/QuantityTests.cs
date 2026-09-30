using Pos.Domain.Common;

namespace Pos.Domain.Tests.Common;

public class QuantityTests
{
    [Fact]
    public void Parse_with_decimals_on_whole_unit_is_TooManyDecimals()
    {
        var result = Quantity.Parse("1.5", 0);

        Assert.Equal(QuantityParseError.TooManyDecimals, result.Error);
    }

    [Fact]
    public void Parse_accepts_three_decimals_but_not_four()
    {
        Assert.Equal(1250, Quantity.Parse("1.250", 3).Value!.Value.Thousandths);
        Assert.Equal(QuantityParseError.TooManyDecimals, Quantity.Parse("1.2505", 3).Error);
    }

    [Fact]
    public void Parse_accepts_thousands_separator_and_whole_numbers()
    {
        Assert.Equal(1_234_000, Quantity.Parse(" 1,234 ", 0).Value!.Value.Thousandths);
    }

    [Theory]
    [InlineData("", QuantityParseError.Empty)]
    [InlineData("abc", QuantityParseError.Format)]
    [InlineData("-1", QuantityParseError.Format)]
    [InlineData("10000000", QuantityParseError.TooLarge)]
    [InlineData("0", QuantityParseError.NotPositive)]
    public void Parse_reports_errors(string text, QuantityParseError expected)
    {
        Assert.Equal(expected, Quantity.Parse(text, 3).Error);
    }

    [Fact]
    public void Parse_allows_zero_when_requested_and_the_maximum_capture()
    {
        Assert.Equal(0, Quantity.Parse("0", 3, allowZero: true).Value!.Value.Thousandths);
        Assert.Equal(Quantity.MaxCaptureThousandths, Quantity.Parse("9,999,999.999", 3).Value!.Value.Thousandths);
    }

    [Fact]
    public void Arithmetic_and_formatting_are_exact()
    {
        var a = Quantity.Parse("0.1", 3).Value!.Value;
        var b = Quantity.Parse("0.2", 3).Value!.Value;

        Assert.Equal(300, (a + b).Thousandths);
        Assert.Equal(100, (b - a).Thousandths);
        Assert.Equal("1.250", Quantity.FromThousandths(1250).ToEditableString(3));
        Assert.Equal("12", Quantity.FromThousandths(12_000).ToEditableString(0));
        Assert.False(Quantity.FromThousandths(1250).FitsDecimals(0));
        Assert.True(Quantity.FromThousandths(2000).FitsDecimals(0));
    }
}
