namespace Pos.Domain.Licensing;

/// <summary>Contenido ya validado del archivo de licencia local (011, data-model).</summary>
/// <param name="Version">Versión del formato del archivo.</param>
/// <param name="MachineId">ID de la máquina a la que pertenece la licencia.</param>
/// <param name="FirstRunUtc">Primer arranque (inicio del período de evaluación).</param>
/// <param name="LastSeenUtc">Última fecha vista; nunca retrocede, así un reloj atrasado no devuelve días.</param>
/// <param name="Grant">Concesión del proveedor vigente; nula durante la evaluación.</param>
public sealed record LicenseRecord(
    int Version,
    string MachineId,
    DateTime FirstRunUtc,
    DateTime LastSeenUtc,
    LicenseGrant? Grant);

/// <summary>Concesión firmada por el proveedor; <paramref name="ValidUntil"/> nulo significa sin vencimiento.</summary>
public sealed record LicenseGrant(
    string MachineId,
    DateTime IssuedAtUtc,
    DateOnly? ValidUntil,
    string Signature);
