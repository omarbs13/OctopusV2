using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Licensing.ImportLicense;
using Pos.Application.Tests.TestSupport;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Licensing;

/// <summary>012, H3: importar suma módulos sin tocar la fecha de inicio; un rechazo no cambia nada.</summary>
public sealed class ImportLicenseHandlerTests
{
    private static readonly DateTime Issued = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    private readonly AuthFixture _auth = new();
    private readonly LicenseState _state;
    private readonly FakeLicenseStore _store = new();
    private readonly FakeSealStore _seals = new();
    private readonly FakeVerifier _verifier = new();
    private readonly DateTime _firstRun;

    public ImportLicenseHandlerTests()
    {
        _state = new LicenseState(_auth.Clock);
        _firstRun = _auth.Clock.UtcNow.AddDays(-40);
        _state.Set(new LicenseRecord(2, "m", _firstRun, _firstRun, 30, new HashSet<LicensedModule>()));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ImportLicenseHandler Handler => new(
        new AccessControl(_auth.Session, _auth.Users, _auth.Grants, NullLogger<AccessControl>.Instance),
        _verifier,
        _store,
        _seals,
        _state,
        new FakeMachine(),
        new FakeAge(),
        _auth.Audit,
        _auth.Clock,
        new GetLicenseStatusHandler(_state, VendorContact.Default, new FakeMachine()),
        NullLogger<ImportLicenseHandler>.Instance);

    private static LicenseVerification.Valid Grant(params LicensedModule[] modules) =>
        new LicenseVerification.Valid(new ExtendedGrant(modules.ToHashSet(), Issued));

    [Fact]
    public async Task LicenciaValida_ActivaElModuloSinReiniciarYSinTocarLaFechaDeInicio()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        Assert.False(_state.IsModuleActive(LicensedModule.Inventory));
        var changed = 0;
        _state.Changed += (_, _) => changed++;
        _verifier.Result = Grant(LicensedModule.Inventory);

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.True(result.IsSuccess);
        Assert.True(_state.IsModuleActive(LicensedModule.Inventory));
        Assert.False(_state.IsModuleActive(LicensedModule.AdvancedReports));
        Assert.Equal(_firstRun, _store.Saved!.FirstRunUtc);
        Assert.Equal(1, changed);
        Assert.Contains("1 módulos", Assert.Single(_auth.Audit.Entries).Details);
    }

    [Fact]
    public async Task ImportarOtraLicencia_SumaModulos_YRepetirEsIdempotente()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        _verifier.Result = Grant(LicensedModule.Inventory);
        await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);
        _verifier.Result = Grant(LicensedModule.AdvancedReports);
        await Handler.HandleAsync(new ImportLicenseCommand("b.poslic"), Ct);
        _verifier.Result = Grant(LicensedModule.Inventory);
        await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.Equal(2, _state.Record!.Modules.Count);
        Assert.Contains(LicensedModule.Inventory, _state.Record.Modules);
        Assert.Contains(LicensedModule.AdvancedReports, _state.Record.Modules);
    }

    [Fact]
    public async Task LicenciaRechazada_DejaElEstadoIntacto()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        _verifier.Result = new LicenseVerification.Rejected(LicenseImportRejection.OtherMachine);

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.Equal(LicenseImportRejection.OtherMachine, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.Empty(_state.Record!.Modules);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public async Task SinPermiso_SeRechaza()
    {
        _auth.SignedIn(_auth.AddUser("caja", UserRole.Cashier));
        _verifier.Result = Grant(LicensedModule.Inventory);

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Null(_store.Saved);
    }

    private sealed class FakeVerifier : ILicenseVerifier
    {
        public LicenseVerification Result { get; set; } = new LicenseVerification.Rejected(LicenseImportRejection.Unreadable);

        public LicenseVerification Verify(string filePath, string machineId) => Result;
    }
}
