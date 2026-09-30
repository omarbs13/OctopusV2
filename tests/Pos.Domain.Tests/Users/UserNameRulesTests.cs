using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class UserNameRulesTests
{
    [Theory]
    [InlineData("Maria", "MARIA")]
    [InlineData("  maria  ", "MARIA")]
    [InlineData("maría", "MARÍA")]
    public void Normalize_NoDistingueMayusculasNiAcentosMayusculizados(string input, string expected)
    {
        Assert.Equal(expected, UserNameRules.Normalize(input));
        Assert.Equal(UserNameRules.Normalize("MARÍA"), UserNameRules.Normalize("maría"));
        Assert.Equal(UserNameRules.Normalize("Maria"), UserNameRules.Normalize("MARIA"));
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("ana.lopez_01-x", true)]
    [InlineData("ab", false)]
    [InlineData("", false)]
    [InlineData("con espacio", false)]
    [InlineData("a@b.com", false)]
    public void IsValid_ReglasDeCaracteresYLongitud(string userName, bool expected)
    {
        Assert.Equal(expected, UserNameRules.IsValid(userName));
    }

    [Fact]
    public void IsValid_LongitudMaximaEsCuarenta()
    {
        Assert.True(UserNameRules.IsValid(new string('a', UserNameRules.UserNameMaxLength)));
        Assert.False(UserNameRules.IsValid(new string('a', UserNameRules.UserNameMaxLength + 1)));
        Assert.False(UserNameRules.IsValid(null));
    }
}
