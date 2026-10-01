using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>011, H3: en modo lectura se bloquean venta, reportes y usuarios; la consulta sigue disponible.</summary>
public sealed class LicenseBlockingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (AccessControl Access, AuthFixture Auth) Build(bool expired)
    {
        var auth = new AuthFixture();
        var state = new LicenseState(auth.Clock);
        var firstRun = auth.Clock.UtcNow.AddDays(expired ? -40 : -3);
        state.Set(new LicenseRecord(1, "m", firstRun, firstRun, null));
        var access = new AccessControl(auth.Session, auth.Users, auth.Grants, NullLogger<AccessControl>.Instance, state);
        auth.SignedIn(auth.AddUser("admin", UserRole.Admin));
        return (access, auth);
    }

    [Theory]
    [InlineData(Permission.Sell)]
    [InlineData(Permission.ViewReports)]
    [InlineData(Permission.ManageUsers)]
    public async Task Vencida_BloqueaVentaReportesYUsuarios(Permission permission)
    {
        var (access, _) = Build(expired: true);

        var decision = await access.CheckAsync(permission, Ct);

        Assert.False(decision.Allowed);
        Assert.IsType<LicenseExpired>(decision.Error);
    }

    [Theory]
    [InlineData(Permission.ViewProducts)]
    [InlineData(Permission.ViewInventory)]
    [InlineData(Permission.ViewAllSales)]
    [InlineData(Permission.OperateShift)]
    [InlineData(Permission.ManageLicense)]
    public async Task Vencida_PermiteConsultarCerrarTurnoYActivar(Permission permission)
    {
        var (access, _) = Build(expired: true);

        Assert.True((await access.CheckAsync(permission, Ct)).Allowed);
    }

    [Fact]
    public async Task EnEvaluacion_TodoSePermite()
    {
        var (access, _) = Build(expired: false);

        Assert.True((await access.CheckAsync(Permission.Sell, Ct)).Allowed);
        Assert.True((await access.CheckAsync(Permission.ViewReports, Ct)).Allowed);
    }
}
