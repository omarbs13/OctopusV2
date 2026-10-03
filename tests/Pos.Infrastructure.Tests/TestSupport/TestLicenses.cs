using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Estados de licencia para las pruebas (025): licencia firmada ya verificada, sin archivos ni firma.</summary>
internal static class TestLicenses
{
    public const string MachineId = "m";

    /// <summary>Licencia vigente con el módulo base (POS) y los módulos indicados, activos desde hace un año.</summary>
    public static LicenseState Licensed(IClock clock, params LicensedModule[] modules) =>
        Exactly(clock, [LicensedModule.Pos, .. modules]);

    /// <summary>Licencia vigente con exactamente los módulos indicados; sin POS el sistema queda bloqueado.</summary>
    public static LicenseState Exactly(IClock clock, params LicensedModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var since = DateOnly.FromDateTime(clock.UtcNow).AddYears(-1);
        return WithGrants(clock, [.. modules.Distinct().Select(m => new ModuleGrant(m, since, null))]);
    }

    /// <summary>Licencia con entradas de módulo arbitrarias (vencimientos, activaciones futuras).</summary>
    public static LicenseState WithGrants(IClock clock, IReadOnlyList<ModuleGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var state = new LicenseState(clock);
        state.Set(Trial(clock), License(clock, grants), storedLicenseRejected: false);
        return state;
    }

    public static SignedLicense License(IClock clock, IReadOnlyList<ModuleGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new SignedLicense(Guid.CreateVersion7(), clock.UtcNow.AddDays(-1), MachineId, "Cliente de prueba", grants);
    }

    /// <summary>Registro de una instalación que ya terminó la prueba e importó una licencia.</summary>
    public static TrialRecord Trial(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var firstRun = clock.UtcNow.AddDays(-60);
        return new TrialRecord(MachineId, firstRun, firstRun, TrialRecord.DefaultTrialDays, firstRun.AddDays(1));
    }
}
