using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>
/// 025, data-model "LicenseEvaluator": prueba de 30 días, licencia firmada, bloqueo sin el módulo base, vigencia
/// inclusiva por módulo y protección contra el reloj atrasado. Fechas en UTC para que el día local sea exacto.
/// </summary>
public sealed class LicenseEvaluatorTests
{
    private static readonly DateTime FirstRun = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TrialRecord Trial(DateTime? lastSeen = null, DateTime? firstRun = null, DateTime? licenseImported = null) =>
        new("m", firstRun ?? FirstRun, lastSeen ?? firstRun ?? FirstRun, TrialRecord.DefaultTrialDays, licenseImported);

    private static SignedLicense License(params ModuleGrant[] grants) =>
        new(Guid.CreateVersion7(), FirstRun, "m", "Cliente", grants);

    private static ModuleGrant Grant(LicensedModule module, DateOnly activates, DateOnly? expires = null) => new(module, activates, expires);

    private static DateOnly Day(int day) => new(2026, 10, day);

    private static DateTime Noon(DateOnly day) => day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    private static LicenseStatus At(TrialRecord trial, SignedLicense? license, DateOnly today, bool storedRejected = false) =>
        LicenseEvaluator.Evaluate(trial, license, storedRejected, Noon(today), TimeZoneInfo.Utc);

    private static LicenseStatus TrialDay(int daysAfterStart, TrialRecord? trial = null) =>
        LicenseEvaluator.Evaluate(trial ?? Trial(), null, false, FirstRun.AddDays(daysAfterStart), TimeZoneInfo.Utc);

    private static LicenseStatus Licensed(DateOnly today, params ModuleGrant[] grants) =>
        At(Trial(lastSeen: Noon(today)), License(grants), today);

    // Prueba (H5)

    [Theory]
    [InlineData(0, 30)]
    [InlineData(12, 18)]
    [InlineData(29, 1)]
    public void Prueba_ActivaLos9ModulosHastaElDia30(int daysAfterStart, int expected)
    {
        var status = TrialDay(daysAfterStart);

        Assert.Equal(LicenseOverall.Trial, status.Overall);
        Assert.Equal(expected, status.TrialDaysRemaining);
        Assert.Equal(ModuleCatalog.All, status.Modules.Select(m => m.Module));
        Assert.All(status.Modules, m => Assert.Equal(ModuleState.Active, m.State));
    }

    [Theory]
    [InlineData(24, LicenseWarning.None)]
    [InlineData(25, LicenseWarning.Near)]
    [InlineData(26, LicenseWarning.None)]
    [InlineData(29, LicenseWarning.Urgent)]
    public void Prueba_AvisaExactamenteCon5Y1Dia(int daysAfterStart, LicenseWarning expected) =>
        Assert.Equal(expected, TrialDay(daysAfterStart).TrialWarning);

    [Fact]
    public void Prueba_ElDia31_BloqueaPorPruebaVencida()
    {
        var status = TrialDay(30);

        Assert.True(status.IsBlocked);
        Assert.Equal(LicenseBlockReason.TrialExpired, status.BlockReason);
        Assert.Equal(0, status.TrialDaysRemaining);
    }

    [Fact]
    public void Prueba_ConFechaDeInicioAlterada_EstaVencida() =>
        Assert.Equal(LicenseBlockReason.TrialExpired, TrialDay(1, Trial(firstRun: DateTime.MinValue, lastSeen: FirstRun)).BlockReason);

    [Fact]
    public void LicenciaPresente_LaPruebaDejaDeAplicarAunqueLeQuedenDias()
    {
        var status = At(Trial(), License(Grant(LicensedModule.Pos, DateOnly.FromDateTime(FirstRun))), DateOnly.FromDateTime(FirstRun).AddDays(3));

        Assert.Equal(LicenseOverall.Licensed, status.Overall);
        Assert.Equal(0, status.TrialDaysRemaining);
        Assert.Equal(ModuleState.NotLicensed, status.StatusOf(LicensedModule.Inventory).State);
    }

    [Fact]
    public void YaLicenciado_SinLicenciaValida_NoVuelveLaPrueba()
    {
        var trial = Trial(licenseImported: FirstRun.AddDays(1));

        var status = TrialDay(9, trial);

        Assert.Equal(LicenseBlockReason.LicenseInvalid, status.BlockReason);
        Assert.All(status.Modules, m => Assert.Equal(ModuleState.NotLicensed, m.State));
    }

    [Fact]
    public void LicenciaGuardadaRechazada_SinMarca_BloqueaPorLicenciaNoValida()
    {
        // Cambio de hardware: el sello ilegible perdió la marca, pero la licencia guardada no se verifica.
        var status = LicenseEvaluator.Evaluate(Trial(), null, storedLicenseRejected: true, FirstRun.AddDays(9), TimeZoneInfo.Utc);

        Assert.Equal(LicenseBlockReason.LicenseInvalid, status.BlockReason);
        Assert.True(status.StoredLicenseRejected);
    }

    // Licencia y módulo base (H6)

    [Fact]
    public void LicenciaConPosActivo_Licenciado_YLosModulosEnOrdenDelCatalogo()
    {
        var status = Licensed(Day(10), Grant(LicensedModule.Inventory, Day(1)), Grant(LicensedModule.Pos, Day(1)));

        Assert.Equal(LicenseOverall.Licensed, status.Overall);
        Assert.Null(status.BlockReason);
        Assert.Equal("Cliente", status.CustomerName);
        Assert.Equal(ModuleCatalog.All, status.Modules.Select(m => m.Module));
        Assert.True(status.IsModuleActive(LicensedModule.Inventory));
        Assert.False(status.IsModuleActive(LicensedModule.Returns));
    }

    [Fact]
    public void LicenciaSinPos_BloqueaAunqueOtrosModulosEstenVigentes()
    {
        var status = Licensed(Day(10), Grant(LicensedModule.Inventory, Day(1)));

        Assert.Equal(LicenseBlockReason.BaseNotLicensed, status.BlockReason);
        Assert.True(status.IsModuleActive(LicensedModule.Inventory));
    }

    [Fact]
    public void PosSoloFuturo_BloqueaPorPosPendiente() =>
        Assert.Equal(LicenseBlockReason.BasePending, Licensed(Day(10), Grant(LicensedModule.Pos, Day(20))).BlockReason);

    [Fact]
    public void PosVencidoAyer_BloqueaPorPosVencido() =>
        Assert.Equal(
            LicenseBlockReason.BaseExpired,
            Licensed(Day(10), Grant(LicensedModule.Pos, Day(1), Day(9)), Grant(LicensedModule.Inventory, Day(1))).BlockReason);

    [Fact]
    public void PosConVencimientoAnteriorALaActivacion_BloqueaPorPosVencido_AunqueLaActivacionSeaFutura() =>
        Assert.Equal(LicenseBlockReason.BaseExpired, Licensed(Day(10), Grant(LicensedModule.Pos, Day(20), Day(15))).BlockReason);

    // Vigencia por módulo (H7)

    [Fact]
    public void Modulo_PendienteAntesDeActivarse_ActivoEseDia_VencidoElDiaSiguienteAlVencimiento()
    {
        var pos = Grant(LicensedModule.Pos, Day(1));
        var credit = Grant(LicensedModule.CreditAndCustomers, Day(20), Day(25));

        Assert.Equal(ModuleState.Pending, Licensed(Day(19), pos, credit).StatusOf(LicensedModule.CreditAndCustomers).State);
        Assert.Equal(ModuleState.Active, Licensed(Day(20), pos, credit).StatusOf(LicensedModule.CreditAndCustomers).State);
        Assert.Equal(ModuleState.Active, Licensed(Day(25), pos, credit).StatusOf(LicensedModule.CreditAndCustomers).State);
        Assert.Equal(ModuleState.Expired, Licensed(Day(26), pos, credit).StatusOf(LicensedModule.CreditAndCustomers).State);
        Assert.Equal(ModuleState.NotLicensed, Licensed(Day(26), pos, credit).StatusOf(LicensedModule.Returns).State);
    }

    [Fact]
    public void VariasEntradas_ActivaSiAlgunaLoEsta_SiNoLaPendienteMasProxima_SiNoLaVencidaMasReciente()
    {
        var pos = Grant(LicensedModule.Pos, Day(1));

        var active = Licensed(Day(10), pos, Grant(LicensedModule.Returns, Day(1), Day(5)), Grant(LicensedModule.Returns, Day(8), Day(12)));
        Assert.Equal(new ModuleStatus(LicensedModule.Returns, ModuleState.Active, Day(8), Day(12)), active.StatusOf(LicensedModule.Returns));

        var pending = Licensed(Day(10), pos, Grant(LicensedModule.Returns, Day(20)), Grant(LicensedModule.Returns, Day(15)));
        Assert.Equal(new ModuleStatus(LicensedModule.Returns, ModuleState.Pending, Day(15), null), pending.StatusOf(LicensedModule.Returns));

        var expired = Licensed(Day(10), pos, Grant(LicensedModule.Returns, Day(1), Day(3)), Grant(LicensedModule.Returns, Day(1), Day(6)));
        Assert.Equal(new ModuleStatus(LicensedModule.Returns, ModuleState.Expired, Day(1), Day(6)), expired.StatusOf(LicensedModule.Returns));
    }

    [Fact]
    public void EntradaConVencimientoAnteriorALaActivacion_SeMuestraVencidaTambienAntesDeActivarse() =>
        Assert.Equal(
            ModuleState.Expired,
            Licensed(Day(10), Grant(LicensedModule.Pos, Day(1)), Grant(LicensedModule.Returns, Day(20), Day(15))).StatusOf(LicensedModule.Returns).State);

    [Fact]
    public void PorVencer_IncluyeDeHoyAHoyMas7_YExcluyeHoyMas8YLosNoActivos()
    {
        var status = Licensed(
            Day(10),
            Grant(LicensedModule.Pos, Day(1)),
            Grant(LicensedModule.Inventory, Day(1), Day(10)),
            Grant(LicensedModule.Returns, Day(1), Day(17)),
            Grant(LicensedModule.Discounts, Day(1), Day(18)),
            Grant(LicensedModule.Categories, Day(11), Day(12)));

        Assert.Equal([LicensedModule.Inventory, LicensedModule.Returns], status.ExpiringSoon.Select(m => m.Module));
    }

    // Reloj atrasado (H8)

    [Fact]
    public void RelojAtrasado_ModuloVencidoSigueVencido_YSeAvisa()
    {
        var license = License(Grant(LicensedModule.Pos, Day(1)), Grant(LicensedModule.Inventory, Day(1), Day(15)));

        var status = At(Trial(lastSeen: Noon(Day(20))), license, Day(10));

        Assert.Equal(ModuleState.Expired, status.StatusOf(LicensedModule.Inventory).State);
        Assert.True(status.ClockBehind);
        Assert.Equal(Day(20), status.LastSeen);
    }

    [Fact]
    public void RelojAtrasado_ModuloQueSeActivaDespues_NoSeActiva()
    {
        var license = License(Grant(LicensedModule.Pos, Day(1)), Grant(LicensedModule.Inventory, Day(18), Day(19)));

        var status = At(Trial(lastSeen: Noon(Day(20))), license, Day(18));

        Assert.False(status.IsModuleActive(LicensedModule.Inventory));
    }

    [Fact]
    public void RelojAtrasadoUnDia_NoAvisa_NiReactiva()
    {
        var license = License(Grant(LicensedModule.Pos, Day(1)), Grant(LicensedModule.Inventory, Day(1), Day(19)));

        var status = At(Trial(lastSeen: Noon(Day(20))), license, Day(19));

        Assert.False(status.ClockBehind);
        Assert.False(status.IsModuleActive(LicensedModule.Inventory));
    }

    [Fact]
    public void RelojCorregido_EvaluaConNormalidad()
    {
        var license = License(Grant(LicensedModule.Pos, Day(1)), Grant(LicensedModule.Inventory, Day(1), Day(25)));

        var status = At(Trial(lastSeen: Noon(Day(20))), license, Day(20));

        Assert.False(status.ClockBehind);
        Assert.True(status.IsModuleActive(LicensedModule.Inventory));
    }

    [Fact]
    public void RelojAtrasado_LaPruebaVencidaNoVuelve_YLosDiasSeCuentanDesdeLaUltimaFechaVista()
    {
        var expired = Trial(lastSeen: FirstRun.AddDays(40));
        Assert.Equal(LicenseBlockReason.TrialExpired, TrialDay(10, expired).BlockReason);

        var running = Trial(lastSeen: FirstRun.AddDays(20));
        var status = TrialDay(10, running);
        Assert.Equal(10, status.TrialDaysRemaining);
        Assert.True(status.ClockBehind);
    }
}
