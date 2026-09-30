using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.SignIn;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Users;

public class SignInHandlerTests
{
    private readonly AuthFixture _auth = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SignInHandler Handler =>
        new(_auth.Verifier, _auth.Session, new SignInValidator(), NullLogger<SignInHandler>.Instance);

    private Task<Result<Pos.Application.Users.SignInOutcome>> SignInAsync(string userName, string password) =>
        Handler.HandleAsync(new SignInCommand(userName, password), Ct);

    [Fact]
    public async Task Correcto_SinDistinguirMayusculasDelUsuario_SellaSinAbrirLaSesion()
    {
        _auth.AddUser("Maria", UserRole.Cashier);

        var result = await SignInAsync("MARIA", AuthFixture.Password);

        Assert.True(result.IsSuccess);
        Assert.Equal("Maria", result.Value.User.UserName);
        Assert.Null(_auth.Session.User);
        Assert.Contains(_auth.Audit.Entries, e => e.Action == AuditActions.LoginSucceeded);
    }

    [Theory]
    [InlineData("noexiste", AuthFixture.Password)]
    [InlineData("ana", "incorrecta")]
    [InlineData("Sistema", AuthFixture.Password)]
    public async Task InexistenteErroneaOSistema_DevuelvenElMismoMensajeGenerico(string userName, string password)
    {
        _auth.AddUser("ana", UserRole.Cashier);

        var result = await SignInAsync(userName, password);

        Assert.IsType<InvalidCredentials>(result.Error);
    }

    [Fact]
    public async Task UsuarioInactivo_DevuelveElMismoMensajeGenericoAunConLaContrasenaCorrecta()
    {
        _auth.AddUser("ana", UserRole.Cashier, active: false);

        var result = await SignInAsync("ana", AuthFixture.Password);

        Assert.IsType<InvalidCredentials>(result.Error);
    }

    [Fact]
    public async Task CincoFallosBloquean_ElSextoConLaContrasenaCorrectaDevuelveLockedOut()
    {
        _auth.AddUser("ana", UserRole.Cashier);

        for (var i = 1; i <= 4; i++)
        {
            Assert.IsType<InvalidCredentials>((await SignInAsync("ana", "mal")).Error);
        }

        var fifth = await SignInAsync("ana", "mal");
        var sixth = await SignInAsync("ana", AuthFixture.Password);

        Assert.IsType<LockedOut>(fifth.Error);
        var locked = Assert.IsType<LockedOut>(sixth.Error);
        Assert.Equal(_auth.Clock.UtcNow.AddMinutes(5), locked.UntilUtc);
        Assert.Contains(_auth.Audit.Entries, e => e.Action == AuditActions.UserLockedOut);

        _auth.Clock.UtcNow = _auth.Clock.UtcNow.AddMinutes(6);
        Assert.True((await SignInAsync("ana", AuthFixture.Password)).IsSuccess);
    }

    [Fact]
    public async Task AccesoCorrecto_ReiniciaElContador()
    {
        var user = _auth.AddUser("ana", UserRole.Cashier);
        for (var i = 0; i < 4; i++)
        {
            await SignInAsync("ana", "mal");
        }

        Assert.True((await SignInAsync("ana", AuthFixture.Password)).IsSuccess);

        Assert.Equal(0, user.FailedLoginCount);
        Assert.IsType<InvalidCredentials>((await SignInAsync("ana", "mal")).Error);
    }

    [Fact]
    public async Task NombreInexistenteRepetido_TambienSeBloqueaSinRevelarLaInexistencia()
    {
        for (var i = 1; i <= 4; i++)
        {
            Assert.IsType<InvalidCredentials>((await SignInAsync("fantasma", "x")).Error);
        }

        Assert.IsType<LockedOut>((await SignInAsync("fantasma", "x")).Error);
        Assert.IsType<LockedOut>((await SignInAsync("fantasma", "x")).Error);
    }

    [Fact]
    public async Task NingunaEntradaDeLaBitacoraContieneLaContrasena()
    {
        _auth.AddUser("ana", UserRole.Cashier);

        await SignInAsync("ana", "clave-secreta-mal");
        await SignInAsync("fantasma", "clave-secreta-mal");
        await SignInAsync("ana", AuthFixture.Password);

        Assert.NotEmpty(_auth.Audit.Entries);
        Assert.All(_auth.Audit.Entries, e =>
        {
            Assert.DoesNotContain("clave-secreta-mal", e.Details ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(AuthFixture.Password, e.Details ?? string.Empty, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CamposVacios_DevuelvenValidacion()
    {
        Assert.IsType<ValidationFailed>((await SignInAsync(" ", AuthFixture.Password)).Error);
        Assert.IsType<ValidationFailed>((await SignInAsync("ana", string.Empty)).Error);
    }
}
