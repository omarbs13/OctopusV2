using Pos.Domain.Common;

namespace Pos.Domain.Tests.Common;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Café Molido", "cafe molido")]
    [InlineData("ÑANDÚ", "nandu")]
    [InlineData("Crème brûlée", "creme brulee")]
    [InlineData("Jalapeño «Extra» 100% & más", "jalapeno «extra» 100% & mas")]
    [InlineData("", "")]
    public void ForSearch_QuitaAcentosYMayusculasConservandoOtrosCaracteres(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.ForSearch(input));
    }
}
