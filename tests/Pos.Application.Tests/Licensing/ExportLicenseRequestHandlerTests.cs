using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Licensing;
using Pos.Application.Licensing.ExportLicenseRequest;
using Pos.Application.Products;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Business;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>
/// 025, H3 (FR-013 a FR-016): conformidad con el contrato compartido <c>contracts/license-request.md</c>, que lee
/// OctopusAdmin; no es una prueba de mapeo. Cualquier usuario con sesión genera la solicitud, también en bloqueo.
/// </summary>
public sealed class ExportLicenseRequestHandlerTests : IDisposable
{
    private readonly AuthFixture _auth = new();
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}.octoreq");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => File.Delete(_path);

    private ExportLicenseRequestHandler Handler(BusinessProfile? profile, ILicenseState? license = null) => new(
        new AccessControl(_auth.Session, _auth.Users, _auth.Grants, NullLogger<AccessControl>.Instance, license),
        new FakeMachine(),
        new FixedAppInfo(),
        _auth.Clock,
        new FixedProfile(profile),
        new FakeCatalogInfo());

    [Fact]
    public async Task Solicitud_CumpleElContrato_SinCifrarNiFirmar_YLaGeneraUnCajeroEnBloqueo()
    {
        _auth.SignedIn(_auth.AddUser("caja", UserRole.Cashier));
        var blocked = new LicenseState(_auth.Clock);
        blocked.Set(Licenses.Trial(_auth.Clock.UtcNow.AddDays(-60), _auth.Clock.UtcNow), null, false);
        Assert.True(blocked.Current.IsBlocked);

        var result = await Handler(BusinessProfile.Create("Abarrotes La Esperanza", "Calle 1", "555", null, null), blocked)
            .HandleAsync(new ExportLicenseRequestCommand(_path), Ct);

        Assert.True(result.IsSuccess);
        var bytes = await File.ReadAllBytesAsync(_path, Ct);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("requestFormat").GetInt32());
        Assert.Equal(FakeMachine.Id, root.GetProperty("machineId").GetString());
        Assert.Equal("Abarrotes La Esperanza", root.GetProperty("businessName").GetString());
        Assert.Equal("1.4.0", root.GetProperty("appVersion").GetString());
        Assert.Equal(1, root.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("2026-09-29T15:30:00Z", root.GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    public async Task SinDatosDelNegocio_ElNombreQuedaVacio()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));

        await Handler(profile: null).HandleAsync(new ExportLicenseRequestCommand(_path), Ct);

        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(_path, Ct));
        Assert.Equal(string.Empty, json.RootElement.GetProperty("businessName").GetString());
    }

    [Fact]
    public async Task SinSesion_SeRechaza()
    {
        var result = await Handler(profile: null).HandleAsync(new ExportLicenseRequestCommand(_path), Ct);

        Assert.IsType<SessionRequired>(result.Error);
        Assert.False(File.Exists(_path));
    }

    private sealed class FixedAppInfo : IAppInfo
    {
        public string Version => "1.4.0";

        public string OperatingSystem => "Linux";
    }

    private sealed class FixedProfile(BusinessProfile? profile) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(profile);

        public void Add(BusinessProfile profile)
        {
        }

        public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(SaveOutcome.Saved);
    }
}
