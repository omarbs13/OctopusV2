using Pos.Domain.Licensing;

namespace Pos.Domain.Tests.Licensing;

/// <summary>012: evaluación de 30 días con todos los módulos y modo modular después.</summary>
public sealed class LicenseEvaluatorTests
{
    private static readonly DateTime FirstRun = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LicenseRecord Record(DateTime? lastSeen = null, params LicensedModule[] modules) =>
        new(2, "m", FirstRun, lastSeen ?? FirstRun, 30, modules.ToHashSet());

    private static LicenseStatus At(LicenseRecord record, int daysAfterStart) =>
        LicenseEvaluator.Evaluate(record, FirstRun.AddDays(daysAfterStart), TimeZoneInfo.Utc);

    [Theory]
    [InlineData(0, 30)]
    [InlineData(12, 18)]
    [InlineData(29, 1)]
    public void Evaluacion_ActivaTodosLosModulosHastaElDia30(int daysAfterStart, int expected)
    {
        var status = At(Record(), daysAfterStart);

        Assert.Equal(LicensePhase.Trial, status.Phase);
        Assert.Equal(expected, status.DaysRemaining);
        Assert.All(ModuleCatalog.All, m => Assert.True(status.IsModuleActive(m)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    public void Dia31_EsModularYSoloQuedanLosComprados(int daysAfterStart)
    {
        var status = At(Record(null, LicensedModule.Inventory), daysAfterStart);

        Assert.Equal(LicensePhase.Modular, status.Phase);
        Assert.Equal(0, status.DaysRemaining);
        Assert.True(status.IsModuleActive(LicensedModule.Inventory));
        Assert.False(status.IsModuleActive(LicensedModule.CashShifts));
        Assert.Equal(LicenseWarning.None, status.Warning);
    }

    [Theory]
    [InlineData(24, LicenseWarning.None)]
    [InlineData(25, LicenseWarning.Near)]
    [InlineData(26, LicenseWarning.None)]
    [InlineData(28, LicenseWarning.None)]
    [InlineData(29, LicenseWarning.Urgent)]
    [InlineData(10, LicenseWarning.None)]
    public void Aviso_SoloConExactamenteCincoYUnDia(int daysAfterStart, LicenseWarning expected) =>
        Assert.Equal(expected, At(Record(), daysAfterStart).Warning);

    [Fact]
    public void RelojRetrasado_NoDevuelveDias()
    {
        var status = At(Record(lastSeen: FirstRun.AddDays(20)), 5);

        Assert.Equal(10, status.DaysRemaining);
    }
}
