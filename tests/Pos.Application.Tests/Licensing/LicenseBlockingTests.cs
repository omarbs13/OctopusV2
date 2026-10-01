using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>012, H4: tras la evaluación se bloquean los módulos no comprados; lo básico sigue disponible.</summary>
public sealed class LicenseBlockingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AccessControl Build(params LicensedModule[] purchased)
    {
        var auth = new AuthFixture();
        var state = new LicenseState(auth.Clock);
        var firstRun = auth.Clock.UtcNow.AddDays(-40);
        state.Set(new LicenseRecord(2, "m", firstRun, firstRun, 30, purchased.ToHashSet()));
        auth.SignedIn(auth.AddUser("admin", UserRole.Admin));
        return new AccessControl(auth.Session, auth.Users, auth.Grants, NullLogger<AccessControl>.Instance, state);
    }

    [Theory]
    [InlineData(Permission.ViewInventory)]
    [InlineData(Permission.ViewReports)]
    [InlineData(Permission.OperateShift)]
    public async Task ModuloSinLicencia_SeRechazaConModuleNotLicensed(Permission permission)
    {
        var access = Build();

        var decision = await access.CheckAsync(permission, Ct);

        Assert.False(decision.Allowed);
        Assert.IsType<ModuleNotLicensed>(decision.Error);
        Assert.False(await access.HasAsync(permission, Ct));
    }

    [Theory]
    [InlineData(Permission.Sell)]
    [InlineData(Permission.ManageUsers)]
    [InlineData(Permission.ViewProducts)]
    [InlineData(Permission.ManageLicense)]
    public async Task FuncionesBasicas_SiguenDisponibles(Permission permission) =>
        Assert.True((await Build().CheckAsync(permission, Ct)).Allowed);

    [Fact]
    public async Task ModuloComprado_SePermiteYLosDemasNo()
    {
        var access = Build(LicensedModule.Inventory);

        Assert.True((await access.CheckAsync(Permission.ViewInventory, Ct)).Allowed);
        Assert.False((await access.CheckAsync(Permission.ViewReports, Ct)).Allowed);
    }
}
