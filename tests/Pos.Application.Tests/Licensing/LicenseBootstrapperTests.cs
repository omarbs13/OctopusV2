using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

/// <summary>012, H1/H2: primer arranque, recuperación desde la copia protegida, reloj atrasado y migración 011.</summary>
public sealed class LicenseBootstrapperTests
{
    private readonly AuthFixture _auth = new();
    private readonly FakeLicenseStore _store = new();
    private readonly FakeSealStore _seals = new();
    private readonly LicenseState _state;

    public LicenseBootstrapperTests() => _state = new LicenseState(_auth.Clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LicenseBootstrapper Build(DateTime? firstUser = null) => new(
        _store,
        _seals,
        _state,
        new FakeMachine(),
        new FakeAge(firstUser),
        new FakeScopeFactory(_auth.Audit),
        _auth.Clock,
        NullLogger<LicenseBootstrapper>.Instance);

    [Fact]
    public async Task PrimerArranque_UsaElMenorEntreAhoraYElPrimerUsuario_YSiembraLaCopia()
    {
        var firstUser = _auth.Clock.UtcNow.AddDays(-10);

        await Build(firstUser).RunAsync(Ct);

        Assert.Equal(firstUser, _state.Record!.FirstRunUtc);
        Assert.Equal(firstUser, _seals.Seal!.FirstRunUtc);
        Assert.Equal(20, _state.Current.DaysRemaining);
        Assert.Empty(_auth.Audit.Entries);
    }

    [Fact]
    public async Task ArchivoBorrado_ConCopiaValida_RecuperaLaFechaSinComprasYAudita()
    {
        var firstRun = _auth.Clock.UtcNow.AddDays(-35);
        _seals.Seal = new LicenseSeal(firstRun, _auth.Clock.UtcNow.AddDays(-1));
        _store.Next = new LicenseLoadResult.Missing();
        var bootstrapper = Build();

        await bootstrapper.RunAsync(Ct);

        Assert.Equal(firstRun, _state.Record!.FirstRunUtc);
        Assert.Empty(_state.Record.Modules);
        Assert.Equal(LicensePhase.Modular, _state.Current.Phase);
        Assert.True(bootstrapper.FileWasRegenerated);
        Assert.Equal(AuditActions.LicenseRecovered, Assert.Single(_auth.Audit.Entries).Action);
    }

    [Fact]
    public async Task ArchivoInutilizable_SeTrataComoBorrado()
    {
        var firstRun = _auth.Clock.UtcNow.AddDays(-5);
        _seals.Seal = new LicenseSeal(firstRun, firstRun);
        _store.Next = new LicenseLoadResult.Unusable();

        await Build().RunAsync(Ct);

        Assert.Equal(firstRun, _state.Record!.FirstRunUtc);
        Assert.NotNull(_store.Saved);
    }

    [Fact]
    public async Task ArchivoBorrado_YRelojAtrasado_NoDevuelveDias()
    {
        var now = _auth.Clock.UtcNow;
        _seals.Seal = new LicenseSeal(now.AddDays(-20), now);
        _auth.Clock.UtcNow = now.AddDays(-10);

        await Build().RunAsync(Ct);

        Assert.Equal(10, _state.Current.DaysRemaining);
    }

    [Fact]
    public async Task ArchivoPresente_ConCopiaMasAntigua_UsaLaFechaDeInicioMasAntigua()
    {
        var now = _auth.Clock.UtcNow;
        _store.Next = new LicenseLoadResult.Loaded(
            new LicenseRecord(2, "m", now.AddDays(-2), now, 30, new HashSet<LicensedModule> { LicensedModule.Inventory }));
        _seals.Seal = new LicenseSeal(now.AddDays(-20), now);

        await Build().RunAsync(Ct);

        Assert.Equal(now.AddDays(-20), _state.Record!.FirstRunUtc);
        Assert.Contains(LicensedModule.Inventory, _state.Record.Modules);
    }

    [Fact]
    public async Task Migracion011_ConConcesionActiva_HabilitaTodosLosModulos()
    {
        var now = _auth.Clock.UtcNow;
        _store.Next = new LicenseLoadResult.LegacyV1(new LegacyLicense(now.AddDays(-100), now, HasGrant: true, ValidUntil: null));

        await Build().RunAsync(Ct);

        Assert.Equal(now.AddDays(-100), _state.Record!.FirstRunUtc);
        Assert.Equal(ModuleCatalog.All.Count, _state.Record.Modules.Count);
    }

    [Fact]
    public async Task Migracion011_SinConcesion_IniciaEvaluacionNueva()
    {
        var now = _auth.Clock.UtcNow;
        _store.Next = new LicenseLoadResult.LegacyV1(new LegacyLicense(now.AddDays(-100), now, HasGrant: false, ValidUntil: null));

        await Build().RunAsync(Ct);

        Assert.Equal(now, _state.Record!.FirstRunUtc);
        Assert.Empty(_state.Record.Modules);
        Assert.Equal(30, _state.Current.DaysRemaining);
        Assert.Equal(now, _seals.Seal!.FirstRunUtc);
    }
}
