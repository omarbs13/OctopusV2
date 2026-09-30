using System.Globalization;
using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Common;

public class MoneyConverterTests
{
    [Theory]
    [InlineData(123450L, "$1,234.50")]
    [InlineData(8950L, "$89.50")]
    [InlineData(0L, "$0.00")]
    [InlineData(99_999_999L, "$999,999.99")]
    public void Convert_CentavosAPesosMexicanos(long cents, string expected)
    {
        Assert.Equal(expected, MoneyConverter.Instance.Convert(cents, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
