namespace Pos.Domain.Licensing;

public enum LicensePhase
{
    /// <summary>Evaluación: todos los módulos activos.</summary>
    Trial,

    /// <summary>Solo los módulos comprados están activos.</summary>
    Modular,
}

public enum LicenseWarning
{
    None,

    /// <summary>Quedan exactamente 5 días.</summary>
    Near,

    /// <summary>Queda exactamente 1 día.</summary>
    Urgent,
}

/// <summary>Estado calculado de la licencia; no se guarda.</summary>
public sealed record LicenseStatus(
    LicensePhase Phase,
    int DaysRemaining,
    LicenseWarning Warning,
    IReadOnlySet<LicensedModule> Modules)
{
    public bool IsModuleActive(LicensedModule module) => Phase == LicensePhase.Trial || Modules.Contains(module);
}
