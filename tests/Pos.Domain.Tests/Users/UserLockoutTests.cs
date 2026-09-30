using Pos.Domain.Users;

namespace Pos.Domain.Tests.Users;

public class UserLockoutTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => User.Create("Ana López", "ana", UserRole.Cashier, "hash");

    [Fact]
    public void CuatroFallosNoBloquean_ElQuintoBloqueaCincoMinutos()
    {
        var user = NewUser();

        for (var i = 1; i <= 4; i++)
        {
            Assert.False(user.RegisterFailedLogin(Now));
            Assert.False(user.IsLockedOut(Now));
        }

        Assert.True(user.RegisterFailedLogin(Now));
        Assert.True(user.IsLockedOut(Now));
        Assert.True(user.IsLockedOut(Now.AddMinutes(4.9)));
        Assert.False(user.IsLockedOut(Now.AddMinutes(5)));
        Assert.Equal(Now.AddMinutes(5), user.LockoutEndsAt);
    }

    [Fact]
    public void AccesoCorrecto_ReiniciaElContador()
    {
        var user = NewUser();
        for (var i = 0; i < 4; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        user.RegisterSuccessfulLogin(Now);

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
        Assert.Equal(Now, user.LastLoginAt);
        Assert.False(user.RegisterFailedLogin(Now));
    }

    [Fact]
    public void TrasVencerElBloqueo_ElSiguienteFalloCuentaDesdeUno()
    {
        var user = NewUser();
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        var later = Now.AddMinutes(6);
        Assert.False(user.RegisterFailedLogin(later));

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
    }
}
