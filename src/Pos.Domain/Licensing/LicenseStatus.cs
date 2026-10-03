namespace Pos.Domain.Licensing;

public enum LicenseOverall
{
    /// <summary>Prueba de 30 días: los 9 módulos activos.</summary>
    Trial,

    /// <summary>Licencia firmada vigente con el módulo base activo.</summary>
    Licensed,

    /// <summary>El módulo base no está activo: solo quedan las operaciones exentas (FR-027, FR-028).</summary>
    Blocked,
}

public enum LicenseBlockReason
{
    /// <summary>Terminó la prueba y nunca se aceptó una licencia.</summary>
    TrialExpired,

    /// <summary>La licencia guardada no supera la verificación, o ya se había licenciado y falta (FR-026a).</summary>
    LicenseInvalid,

    /// <summary>La licencia no incluye el módulo base.</summary>
    BaseNotLicensed,

    /// <summary>El módulo base aún no llega a su fecha de activación.</summary>
    BasePending,

    /// <summary>El módulo base venció.</summary>
    BaseExpired,
}

public enum ModuleState
{
    Active,
    Pending,
    Expired,
    NotLicensed,
}

public enum LicenseWarning
{
    None,

    /// <summary>Quedan exactamente 5 días de prueba.</summary>
    Near,

    /// <summary>Queda exactamente 1 día de prueba.</summary>
    Urgent,
}

/// <summary>Estado de un módulo con las fechas de la entrada que lo determina.</summary>
public sealed record ModuleStatus(LicensedModule Module, ModuleState State, DateOnly? ActivatesOn, DateOnly? ExpiresOn);

/// <summary>Estado calculado de la licencia (025, data-model); no se guarda.</summary>
/// <param name="Overall">Estado general.</param>
/// <param name="BlockReason">Causa; solo con <see cref="LicenseOverall.Blocked"/>.</param>
/// <param name="TrialDaysRemaining">0 fuera de la prueba.</param>
/// <param name="TrialWarning">Aviso de prueba por vencer.</param>
/// <param name="CustomerName">Cliente de la licencia vigente.</param>
/// <param name="Modules">Los 9 módulos en el orden del catálogo.</param>
/// <param name="ExpiringSoon">Módulos activos que vencen en los próximos 7 días (incluido hoy).</param>
/// <param name="ClockBehind">El reloj está más de 1 día antes de la última fecha vista.</param>
/// <param name="LastSeen">Última fecha vista (fecha local), para el aviso de reloj atrasado.</param>
/// <param name="StoredLicenseRejected">Había licencia guardada pero no pasó la reverificación.</param>
public sealed record LicenseStatus(
    LicenseOverall Overall,
    LicenseBlockReason? BlockReason,
    int TrialDaysRemaining,
    LicenseWarning TrialWarning,
    string? CustomerName,
    IReadOnlyList<ModuleStatus> Modules,
    IReadOnlyList<ModuleStatus> ExpiringSoon,
    bool ClockBehind,
    DateOnly LastSeen,
    bool StoredLicenseRejected)
{
    public bool IsBlocked => Overall == LicenseOverall.Blocked;

    /// <summary>Estado individual del módulo; con el sistema bloqueado, <see cref="IsBlocked"/> manda sobre él.</summary>
    public bool IsModuleActive(LicensedModule module) => StatusOf(module).State == ModuleState.Active;

    public ModuleStatus StatusOf(LicensedModule module)
    {
        foreach (var status in Modules)
        {
            if (status.Module == module)
            {
                return status;
            }
        }

        return new ModuleStatus(module, ModuleState.NotLicensed, null, null);
    }
}
