using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Licensing.ImportLicense;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>
/// 025, H4 (FR-017 a FR-022, FR-026a): verificar en orden (firma, máquina, antigüedad), reemplazar sin sumar,
/// aplicar sin reiniciar y conservar la licencia vigente ante cualquier rechazo.
/// </summary>
public sealed class ImportLicenseHandlerTests : IDisposable
{
    private static readonly DateTime Issued = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    private readonly AuthFixture _auth = new();
    private readonly LicenseState _state;
    private readonly FakeLicenseStore _store = new();
    private readonly FakeSealStore _seals = new();
    private readonly FakeInstalledLicenseStore _installed = new();
    private readonly FakeVerifier _verifier = new();
    private readonly List<string> _files = [];
    private readonly DateTime _firstRun;

    public ImportLicenseHandlerTests()
    {
        _state = new LicenseState(_auth.Clock);
        _firstRun = _auth.Clock.UtcNow.AddDays(-10);
        _state.Set(Licenses.Trial(_firstRun, _auth.Clock.UtcNow), null, false);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ImportLicenseHandler Handler => new(
        new AccessControl(_auth.Session, _auth.Users, _auth.Grants, NullLogger<AccessControl>.Instance, _state),
        _verifier,
        _installed,
        _state,
        new LicenseBootstrapper(
            _store,
            _seals,
            _installed,
            _verifier,
            _state,
            new FakeMachine(),
            new FakeAge(),
            new FakeScopeFactory(_auth.Audit),
            _auth.Clock,
            NullLogger<LicenseBootstrapper>.Instance),
        new FakeMachine(),
        _auth.Audit,
        _auth.Clock,
        new GetLicenseStatusHandler(_state, VendorContact.Default, new FakeMachine(), new FakeCatalogInfo()),
        NullLogger<ImportLicenseHandler>.Instance);

    public void Dispose()
    {
        foreach (var file in _files)
        {
            File.Delete(file);
        }
    }

    private string FileWith(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos-test-{Guid.NewGuid():N}.lic");
        File.WriteAllText(path, content);
        _files.Add(path);
        return path;
    }

    private string FileFor(SignedLicense license) => FileWith(_verifier.Register(license));

    private Task<Result<LicenseStatusDto>> ImportAsync(string path) => Handler.HandleAsync(new ImportLicenseCommand(path), Ct);

    private void SignedInAsAdmin() => _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));

    [Fact]
    public async Task LicenciaValida_AplicaExactamenteSusModulosSinReiniciar_YTerminaLaPrueba()
    {
        SignedInAsAdmin();
        var changed = 0;
        _state.Changed += (_, _) => changed++;
        var file = FileFor(Licenses.Issued(Issued, LicensedModule.Pos, LicensedModule.Inventory));

        var result = await ImportAsync(file);

        Assert.True(result.IsSuccess);
        Assert.Equal(LicenseOverall.Licensed, result.Value.Overall);
        Assert.Equal(
            [LicensedModule.Pos, LicensedModule.Inventory],
            _state.Current.Modules.Where(m => m.State == ModuleState.Active).Select(m => m.Module));
        Assert.Equal(1, changed);
        Assert.Equal(File.ReadAllText(file), _installed.Content);
        Assert.Equal(_auth.Clock.UtcNow, _state.Trial!.LicenseImportedUtc);
        Assert.Equal(_auth.Clock.UtcNow, _seals.Seal!.LicenseImportedUtc);
        Assert.Equal(_auth.Clock.UtcNow, _store.Saved!.LicenseImportedUtc);
        Assert.Equal(AuditActions.LicenseImported, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Fact]
    public async Task LicenciaMasReciente_ReemplazaALaAnterior_NoSeSuma()
    {
        SignedInAsAdmin();
        await ImportAsync(FileFor(Licenses.Issued(Issued, LicensedModule.Pos, LicensedModule.Inventory, LicensedModule.Returns)));

        var result = await ImportAsync(FileFor(Licenses.Issued(Issued.AddHours(1), LicensedModule.Pos, LicensedModule.Inventory)));

        Assert.True(result.IsSuccess);
        Assert.True(_state.IsModuleActive(LicensedModule.Inventory));
        Assert.False(_state.IsModuleActive(LicensedModule.Returns));
    }

    [Fact]
    public async Task Reimportacion_DelMismoId_SeAcepta_SinCambios()
    {
        SignedInAsAdmin();
        var license = Licenses.Issued(Issued, LicensedModule.Pos, LicensedModule.Inventory);
        var file = FileFor(license);
        await ImportAsync(file);
        var imported = _state.Trial!.LicenseImportedUtc;
        _auth.Clock.UtcNow = _auth.Clock.UtcNow.AddHours(2);

        var result = await ImportAsync(file);

        Assert.True(result.IsSuccess);
        Assert.Equal(license, _state.License);
        Assert.Equal(imported, _state.Trial!.LicenseImportedUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task OtroIdConEmisionIgualOAnterior_NoEsMasReciente_YSeConservaLaVigente(int seconds)
    {
        SignedInAsAdmin();
        var current = Licenses.Issued(Issued, LicensedModule.Pos, LicensedModule.Inventory);
        await ImportAsync(FileFor(current));
        _auth.Audit.Entries.Clear();

        var result = await ImportAsync(FileFor(Licenses.Issued(Issued.AddSeconds(seconds), LicensedModule.Pos, LicensedModule.Returns)));

        Assert.Equal(LicenseImportRejection.NotNewer, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.Equal(current, _state.License);
        Assert.Equal(1, _installed.Replacements);
        Assert.Equal(AuditActions.LicenseRejectedOnImport, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Theory]
    [InlineData(LicenseImportRejection.BadSignature)]
    [InlineData(LicenseImportRejection.UnsupportedFormat)]
    [InlineData(LicenseImportRejection.Unreadable)]
    public async Task Rechazo_DelVerificador_ConservaLaLicenciaVigenteYElAlmacen(LicenseImportRejection reason)
    {
        SignedInAsAdmin();
        var current = Licenses.Issued(Issued, LicensedModule.Pos);
        await ImportAsync(FileFor(current));
        _verifier.Rejection = reason;

        var result = await ImportAsync(FileWith("alterada"));

        Assert.Equal(reason, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.Equal(current, _state.License);
        Assert.Equal(1, _installed.Replacements);
    }

    [Fact]
    public async Task LicenciaDeOtraMaquina_SeRechazaAntesQueLaAntiguedad()
    {
        SignedInAsAdmin();
        await ImportAsync(FileFor(Licenses.Issued(Issued, LicensedModule.Pos)));
        var other = Licenses.Issued(Issued.AddDays(-1), LicensedModule.Pos) with { MachineId = "otra" };

        var result = await ImportAsync(FileFor(other));

        Assert.Equal(LicenseImportRejection.OtherMachine, Assert.IsType<InvalidLicense>(result.Error).Reason);
    }

    [Fact]
    public async Task ArchivoInexistente_EsIlegible()
    {
        SignedInAsAdmin();

        var result = await ImportAsync(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lic"));

        Assert.Equal(LicenseImportRejection.Unreadable, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.Empty(_verifier.Verified);
    }

    [Fact]
    public async Task SinPermiso_SeRechaza_SinLeerNiGuardar()
    {
        _auth.SignedIn(_auth.AddUser("caja", UserRole.Cashier));

        var result = await ImportAsync(FileFor(Licenses.Issued(Issued, LicensedModule.Pos)));

        Assert.IsType<Forbidden>(result.Error);
        Assert.Null(_installed.Content);
        Assert.Empty(_verifier.Verified);
    }

    [Fact]
    public async Task EnBloqueo_ElAdministradorPuedeImportar_YElBloqueoSeLevantaDeInmediato()
    {
        SignedInAsAdmin();
        _state.Set(Licenses.Trial(_auth.Clock.UtcNow.AddDays(-60), _auth.Clock.UtcNow), null, false);
        Assert.True(_state.Current.IsBlocked);

        var result = await ImportAsync(FileFor(Licenses.Issued(Issued, LicensedModule.Pos)));

        Assert.True(result.IsSuccess);
        Assert.False(_state.Current.IsBlocked);
    }
}
