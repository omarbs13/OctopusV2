using Pos.Application.Users.AuthorizeAdmin;
using Pos.Application.Users.ChangeOwnPassword;
using Pos.Application.Users.CreateFirstAdmin;
using Pos.Application.Users.CreateUser;
using Pos.Application.Users.ResetUserPassword;
using Pos.Application.Users.SignIn;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Users;

/// <summary>SC-008 y Principio VIII: ningún comando con contraseña la expone en <c>ToString</c> (logs, excepciones).</summary>
public class SecretsNotExposedTests
{
    private const string Secret = "clave-super-secreta";

    public static TheoryData<object> Commands() =>
    [
        new SignInCommand("ana", Secret),
        new CreateFirstAdminCommand("Ana", "ana", Secret, Secret),
        new AuthorizeAdminCommand(Permission.CancelSales, "admin", Secret),
        new ResetUserPasswordCommand(Guid.NewGuid(), Secret, Secret),
        new CreateUserCommand("Ana", "ana", UserRole.Cashier, true, Secret, Secret),
        new ChangeOwnPasswordCommand(Secret, Secret + "2", Secret + "2"),
    ];

    [Theory]
    [MemberData(nameof(Commands))]
    public void ToString_NoContieneLaContrasena(object command)
    {
        Assert.DoesNotContain("secreta", command.ToString(), StringComparison.Ordinal);
    }
}
