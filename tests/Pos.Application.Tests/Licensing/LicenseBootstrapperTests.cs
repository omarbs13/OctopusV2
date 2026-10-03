using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

/// <summary>
/// 025, H4/H5: reconciliación del registro de prueba con la copia protegida, reverificación de la licencia
/// guardada en cada arranque (FR-020) y fin definitivo de la prueba tras licenciar (FR-026a).
/// </summary>
public sealed class LicenseBootstrapperTests
{
    private readonly AuthFixture _auth = new();
    private readonly FakeLicenseStore _store = new();
    private readonly FakeSealStore _seals = new();
    private readonly FakeInstalledLicenseStore _installed = new();
    private readonly FakeVerifier _verifier = new();
    private readonly LicenseState _state;

    public LicenseBootstrapperTests() => _state = new LicenseState(_auth.Clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => _auth.Clock.UtcNow;

    private LicenseBootstrapper Build(DateTime? firstUser = null) => new(
        _store,
        _seals,
        _installed,
        _verifier,
        _state,
        new FakeMachine(),
        new FakeAge(firstUser),
        new FakeScopeFactory(_auth.Audit),
        _auth.Clock,
        NullLogger<LicenseBootstrapper>.Instance);

    private void Loaded(TrialRecord record, bool hadLegacyModules = false) =>
        _store.Next = new LicenseLoadResult.Loaded(record, hadLegacyModules);

    // Reconciliación de la prueba (H5)

    [Fact]
    public async Task PrimerArranque_UsaElMenorEntreAhoraYElPrimerUsuario_YSiembraLaCopia()
    {
        var firstUser = Now.AddDays(-10);

        await Build(firstUser).RunAsync(Ct);

        Assert.Equal(firstUser, _state.Trial!.FirstRunUtc);
        Assert.Equal(firstUser, _seals.Seal!.FirstRunUtc);
        Assert.Equal(20, _state.Current.TrialDaysRemaining);
        Assert.Empty(_auth.Audit.Entries);
    }

    [Fact]
    public async Task SoloElArchivoAlterado_SeRecuperaDesdeLaCopia_YSeAudita()
    {
        var firstRun = Now.AddDays(-5);
        _seals.Result = new LicenseSealReadResult.Valid(new LicenseSeal(firstRun, Now.AddDays(-1)));
        _store.Next = new LicenseLoadResult.Unusable();
        var bootstrapper = Build();

        await bootstrapper.RunAsync(Ct);

        Assert.Equal(firstRun, _state.Trial!.FirstRunUtc);
        Assert.Equal(25, _state.Current.TrialDaysRemaining);
        Assert.True(bootstrapper.FileWasRegenerated);
        Assert.NotNull(_store.Saved);
        Assert.Equal(AuditActions.LicenseRecovered, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Fact]
    public async Task SelloAlterado_VenceLaPrueba()
    {
        Loaded(Licenses.Trial(Now.AddDays(-2), Now));
        _seals.Result = new LicenseSealReadResult.Tampered();

        await Build().RunAsync(Ct);

        Assert.Equal(DateTime.MinValue, _state.Trial!.FirstRunUtc);
        Assert.Equal(LicenseBlockReason.TrialExpired, _state.Current.BlockReason);
    }

    [Fact]
    public async Task FaltanArchivoYSello_UsaLaEvidenciaDelPrimerUsuario()
    {
        await Build(Now.AddDays(-40)).RunAsync(Ct);

        Assert.Equal(LicenseBlockReason.TrialExpired, _state.Current.BlockReason);
    }

    [Fact]
    public async Task InstalacionExistente_ConservaLaFechaDeInicioMasAntigua_YLaMarcaDeLicencia()
    {
        var imported = Now.AddDays(-15);
        Loaded(Licenses.Trial(Now.AddDays(-2), Now));
        _seals.Result = new LicenseSealReadResult.Valid(new LicenseSeal(Now.AddDays(-20), Now, imported));

        await Build().RunAsync(Ct);

        Assert.Equal(Now.AddDays(-20), _state.Trial!.FirstRunUtc);
        Assert.Equal(imported, _state.Trial.LicenseImportedUtc);
        Assert.Equal(imported, _seals.Seal!.LicenseImportedUtc);
    }

    [Fact]
    public async Task RelojAtrasado_LaUltimaFechaVistaNoRetrocede()
    {
        Loaded(Licenses.Trial(Now.AddDays(-20), Now));
        _auth.Clock.UtcNow = Now.AddDays(-10);

        await Build().RunAsync(Ct);

        Assert.Equal(10, _state.Current.TrialDaysRemaining);
        Assert.True(_state.Current.ClockBehind);
    }

    [Fact]
    public async Task ArchivoDePruebaV2ConModulos_SeTrataComoLicenciaGuardadaInvalida()
    {
        Loaded(Licenses.Trial(Now.AddDays(-3), Now), hadLegacyModules: true);

        await Build().RunAsync(Ct);

        Assert.True(_state.Current.StoredLicenseRejected);
        Assert.Equal(LicenseBlockReason.LicenseInvalid, _state.Current.BlockReason);
        Assert.Equal(Now, _state.Trial!.LicenseImportedUtc);
        Assert.Equal(AuditActions.LicenseStoredRejected, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Fact]
    public async Task ArchivoDePruebaV2SinModulos_SigueLaPrueba()
    {
        Loaded(Licenses.Trial(Now.AddDays(-3), Now));

        await Build().RunAsync(Ct);

        Assert.Equal(LicenseOverall.Trial, _state.Current.Overall);
        Assert.Empty(_auth.Audit.Entries);
    }

    // Licencia guardada (H4)

    [Fact]
    public async Task LicenciaGuardadaValida_Licenciado()
    {
        Loaded(Licenses.Trial(Now.AddDays(-40), Now, Now.AddDays(-20)));
        var license = Licenses.Issued(Now.AddDays(-20), LicensedModule.Pos, LicensedModule.Inventory);
        _installed.Content = _verifier.Register(license);

        await Build().RunAsync(Ct);

        Assert.Equal(LicenseOverall.Licensed, _state.Current.Overall);
        Assert.Equal(license, _state.License);
        Assert.False(_state.Current.StoredLicenseRejected);
    }

    [Fact]
    public async Task LicenciaGuardadaAlterada_NoHabilitaModulos_NiVuelveLaPrueba_YSeRegistra()
    {
        Loaded(Licenses.Trial(Now.AddDays(-3), Now, Now.AddDays(-2)));
        _installed.Content = "editada a mano";

        await Build().RunAsync(Ct);

        Assert.Null(_state.License);
        Assert.True(_state.Current.StoredLicenseRejected);
        Assert.Equal(LicenseBlockReason.LicenseInvalid, _state.Current.BlockReason);
        Assert.All(_state.Current.Modules, m => Assert.NotEqual(ModuleState.Active, m.State));
        Assert.Equal(AuditActions.LicenseStoredRejected, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Fact]
    public async Task LicenciaGuardadaDeOtraMaquina_ConMarcaPerdida_BloqueaPorLicenciaNoValida()
    {
        // Cambio de hardware: el sello no se puede descifrar y el archivo es de otra máquina.
        _store.Next = new LicenseLoadResult.Unusable();
        _seals.Result = new LicenseSealReadResult.Tampered();
        _installed.Content = _verifier.Register(Licenses.Issued(Now.AddDays(-20), LicensedModule.Pos) with { MachineId = "anterior" });

        await Build().RunAsync(Ct);

        Assert.Equal(LicenseBlockReason.LicenseInvalid, _state.Current.BlockReason);
    }

    [Fact]
    public async Task FilaDeLicenciaBorrada_ConMarcaFijada_BloqueaPorLicenciaNoValida()
    {
        Loaded(Licenses.Trial(Now.AddDays(-3), Now, Now.AddDays(-2)));

        await Build().RunAsync(Ct);

        Assert.Equal(LicenseBlockReason.LicenseInvalid, _state.Current.BlockReason);
        Assert.False(_state.Current.StoredLicenseRejected);
    }

    [Fact]
    public async Task LicenciaGuardadaValida_SinMarca_LaFijaAlArrancar()
    {
        // Un corte entre guardar la licencia y el sello dejó la marca sin fijar.
        Loaded(Licenses.Trial(Now.AddDays(-3), Now));
        _installed.Content = _verifier.Register(Licenses.Issued(Now.AddDays(-1), LicensedModule.Pos));

        await Build().RunAsync(Ct);

        Assert.Equal(LicenseOverall.Licensed, _state.Current.Overall);
        Assert.Equal(Now, _state.Trial!.LicenseImportedUtc);
        Assert.Equal(Now, _seals.Seal!.LicenseImportedUtc);
    }

    [Fact]
    public async Task Touch_AvanzaLaUltimaFechaVista_YReevalua()
    {
        Loaded(Licenses.Trial(Now.AddDays(-28), Now));
        await Build().RunAsync(Ct);
        var changed = 0;
        _state.Changed += (_, _) => changed++;
        _auth.Clock.UtcNow = Now.AddDays(1);

        await Build().TouchAsync();

        Assert.Equal(Now, _state.Trial!.LastSeenUtc);
        Assert.Equal(Now, _seals.Seal!.LastSeenUtc);
        Assert.Equal(LicenseWarning.Urgent, _state.Current.TrialWarning);
        Assert.Equal(1, changed);
    }
}
