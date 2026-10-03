using Pos.Application.Licensing;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

/// <summary>012, H5: avisos solo con exactamente 5 y 1 día; el DTO trae fase, días y módulos activos.</summary>
public sealed class GetLicenseStatusHandlerTests
{
    private static LicenseStatusDto At(int daysSinceStart)
    {
        var clock = new FakeClock();
        var state = new LicenseState(clock);
        var firstRun = clock.UtcNow.AddDays(-daysSinceStart);
        state.Set(new LicenseRecord(2, "m", firstRun, firstRun, 30, new HashSet<LicensedModule> { LicensedModule.Returns }));
        return new GetLicenseStatusHandler(state, VendorContact.Default, new FakeMachine()).Handle();
    }

    [Theory]
    [InlineData(25, LicenseWarning.Near)]
    [InlineData(29, LicenseWarning.Urgent)]
    [InlineData(26, LicenseWarning.None)]
    [InlineData(27, LicenseWarning.None)]
    [InlineData(28, LicenseWarning.None)]
    [InlineData(10, LicenseWarning.None)]
    [InlineData(31, LicenseWarning.None)]
    public void Aviso_SoloConCincoYUnDia(int daysSinceStart, LicenseWarning expected) =>
        Assert.Equal(expected, At(daysSinceStart).Warning);

    [Fact]
    public void EnEvaluacion_TodosLosModulosEstanActivos()
    {
        var dto = At(3);

        Assert.Equal(LicensePhase.Trial, dto.Phase);
        Assert.Equal(27, dto.DaysRemaining);
        Assert.Equal(ModuleCatalog.All.Count, dto.ActiveModules.Count);
    }

    [Fact]
    public void EnModoModular_SoloLosComprados()
    {
        var dto = At(40);

        Assert.Equal(LicensePhase.Modular, dto.Phase);
        Assert.Equal([LicensedModule.Returns], dto.ActiveModules);
    }
}
