using Pos.Application.Licensing;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Licensing;

namespace Pos.Application.Tests.Licensing;

/// <summary>025, FR-034 y research §9: el estado cambia al cambiar de día y solo avisa cuando cambia la huella.</summary>
public sealed class LicenseStateTests
{
    [Fact]
    public void CambiarDeDia_ActivaElModulo_YRefreshAvisaSoloSiCambioLaHuella()
    {
        var clock = new FakeClock { UtcNow = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc) };
        var state = new LicenseState(clock);
        var day20 = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, TimeZoneInfo.Local)).AddDays(1);
        state.Set(
            Licenses.Trial(clock.UtcNow.AddDays(-60), clock.UtcNow, clock.UtcNow.AddDays(-50)),
            new SignedLicense(
                Guid.CreateVersion7(),
                clock.UtcNow.AddDays(-1),
                FakeMachine.Id,
                "C",
                [new ModuleGrant(LicensedModule.Pos, new DateOnly(2026, 1, 1), null), new ModuleGrant(LicensedModule.CreditAndCustomers, day20, null)]),
            false);
        var changed = 0;
        state.Changed += (_, _) => changed++;

        state.Refresh();
        Assert.Equal(0, changed);
        Assert.False(state.IsModuleActive(LicensedModule.CreditAndCustomers));

        clock.UtcNow = clock.UtcNow.AddDays(1);
        Assert.True(state.IsModuleActive(LicensedModule.CreditAndCustomers));

        state.Refresh();
        state.Refresh();
        Assert.Equal(1, changed);
    }
}
