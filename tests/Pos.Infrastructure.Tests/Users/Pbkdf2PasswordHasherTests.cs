using Pos.Application.Users;
using Pos.Infrastructure.Security;

namespace Pos.Infrastructure.Tests.Users;

public class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void VerificaLaContrasenaCorrecta_YRechazaLaErronea()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 1000);
        var hash = hasher.Hash("contraseña-segura");

        Assert.Equal(PasswordCheck.Succeeded, hasher.Verify("contraseña-segura", hash));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("otra-contraseña", hash));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("contraseña-segura", "texto-que-no-es-un-hash"));
    }

    [Fact]
    public void ElHashNoContieneLaContrasenaEnClaro_YCadaHashLlevaSuPropiaSal()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 1000);

        var first = hasher.Hash("contraseña-segura");
        var second = hasher.Hash("contraseña-segura");

        Assert.DoesNotContain("contraseña-segura", first, StringComparison.Ordinal);
        Assert.StartsWith("pbkdf2-sha256$1000$", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ConMenosIteracionesQueLasVigentes_PidePonerAlDiaElHash()
    {
        var old = new Pbkdf2PasswordHasher(iterations: 1000).Hash("clave");

        Assert.Equal(PasswordCheck.SucceededRehashNeeded, new Pbkdf2PasswordHasher(iterations: 2000).Verify("clave", old));
        Assert.Equal(PasswordCheck.Succeeded, new Pbkdf2PasswordHasher(iterations: 1000).Verify("clave", old));
    }

    [Fact]
    public void ElCostoPorDefectoEs600000Iteraciones()
    {
        var hash = new Pbkdf2PasswordHasher().Hash("clave");

        Assert.StartsWith("pbkdf2-sha256$600000$", hash, StringComparison.Ordinal);
    }
}
