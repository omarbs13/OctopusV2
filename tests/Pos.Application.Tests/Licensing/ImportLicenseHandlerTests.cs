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

/// <summary>011, H4: importar una licencia la aplica sin reiniciar; un rechazo no cambia nada.</summary>
public sealed class ImportLicenseHandlerTests
{
    private static readonly DateTime Issued = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    private readonly AuthFixture _auth = new();
    private readonly LicenseState _state;
    private readonly FakeStore _store = new();
    private readonly FakeVerifier _verifier = new();

    public ImportLicenseHandlerTests()
    {
        _state = new LicenseState(_auth.Clock);
        var firstRun = _auth.Clock.UtcNow.AddDays(-40);
        _state.Set(new LicenseRecord(1, "m", firstRun, firstRun, null));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ImportLicenseHandler Handler => new(
        new AccessControl(_auth.Session, _auth.Users, _auth.Grants, NullLogger<AccessControl>.Instance),
        _verifier,
        _store,
        _state,
        new FakeMachine(),
        new FakeAge(),
        _auth.Audit,
        _auth.Clock,
        new GetLicenseStatusHandler(_state, VendorContact.Default),
        NullLogger<ImportLicenseHandler>.Instance);

    [Fact]
    public async Task LicenciaValida_LevantaElModoLecturaSinReiniciar()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        Assert.True(_state.Current.IsReadOnly);
        _verifier.Result = new LicenseVerification.Valid(new LicenseGrant("m", Issued, null, "firma"));

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(_state.Current.IsReadOnly);
        Assert.NotNull(_store.Saved);
        Assert.Single(_auth.Audit.Entries);
    }

    [Fact]
    public async Task LicenciaRechazada_DejaElEstadoIntacto()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        _verifier.Result = new LicenseVerification.Rejected(LicenseImportRejection.OtherMachine);

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.Equal(LicenseImportRejection.OtherMachine, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.True(_state.Current.IsReadOnly);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public async Task LicenciaMasAntiguaQueLaVigente_SeRechaza()
    {
        _auth.SignedIn(_auth.AddUser("admin", UserRole.Admin));
        var current = _state.Record! with { Grant = new LicenseGrant("m", Issued, new DateOnly(2026, 10, 1), "firma") };
        _state.Set(current);
        _verifier.Result = new LicenseVerification.Valid(new LicenseGrant("m", Issued.AddDays(-30), null, "firma"));

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.Equal(LicenseImportRejection.Older, Assert.IsType<InvalidLicense>(result.Error).Reason);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public async Task SinPermiso_SeRechaza()
    {
        _auth.SignedIn(_auth.AddUser("caja", UserRole.Cashier));
        _verifier.Result = new LicenseVerification.Valid(new LicenseGrant("m", Issued, null, "firma"));

        var result = await Handler.HandleAsync(new ImportLicenseCommand("a.poslic"), Ct);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Null(_store.Saved);
    }

    private sealed class FakeStore : ILicenseStore
    {
        public LicenseRecord? Saved { get; private set; }

        public LicenseLoadResult Load() => new LicenseLoadResult.Missing();

        public void Save(LicenseRecord record) => Saved = record;
    }

    private sealed class FakeVerifier : ILicenseVerifier
    {
        public LicenseVerification Result { get; set; } = new LicenseVerification.Rejected(LicenseImportRejection.Unreadable);

        public LicenseVerification Verify(string filePath, string machineId) => Result;
    }

    private sealed class FakeMachine : IMachineIdProvider
    {
        public string GetMachineId() => "m";
    }

    private sealed class FakeAge : IInstallationAgeReader
    {
        public Task<DateTime?> GetFirstUserCreatedUtcAsync(CancellationToken cancellationToken) => Task.FromResult<DateTime?>(null);
    }
}
