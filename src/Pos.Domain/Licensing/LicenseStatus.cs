namespace Pos.Domain.Licensing;

public enum LicenseKind
{
    /// <summary>Período de evaluación con días restantes.</summary>
    Trial,

    /// <summary>Licencia otorgada por el proveedor y vigente.</summary>
    Licensed,

    /// <summary>Venció la evaluación o la licencia: modo lectura.</summary>
    Expired,

    /// <summary>El archivo de licencia es de otra máquina o está dañado: modo lectura.</summary>
    Invalid,
}

public enum LicenseWarning
{
    None,

    /// <summary>Quedan 5 días o menos.</summary>
    Near,

    /// <summary>Queda 1 día o menos.</summary>
    Urgent,
}

public enum InvalidLicenseReason
{
    OtherMachine,
    Corrupt,
}

/// <summary>Estado calculado de la licencia; no se guarda.</summary>
public sealed record LicenseStatus(
    LicenseKind Kind,
    int? DaysRemaining,
    bool IsReadOnly,
    LicenseWarning Warning,
    InvalidLicenseReason? InvalidReason = null)
{
    public static LicenseStatus Invalid(InvalidLicenseReason reason) =>
        new(LicenseKind.Invalid, null, true, LicenseWarning.None, reason);
}
