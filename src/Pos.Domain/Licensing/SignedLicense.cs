namespace Pos.Domain.Licensing;

/// <summary>
/// Contenido verificado de una licencia formato 3 (025, contracts/license-format.md §3). La firma no
/// forma parte: vive en el texto guardado tal como se recibió.
/// </summary>
public sealed record SignedLicense(
    Guid LicenseId,
    DateTime IssuedAtUtc,
    string MachineId,
    string CustomerName,
    IReadOnlyList<ModuleGrant> Grants)
{
    /// <summary>
    /// Regla de antigüedad (FR-018, contrato §4.6): se acepta si no hay licencia vigente, si es la misma
    /// (reimportación) o si se emitió estrictamente después.
    /// </summary>
    public bool IsAcceptableReplacementFor(SignedLicense? current) =>
        current is null || LicenseId == current.LicenseId || IssuedAtUtc > current.IssuedAtUtc;
}
