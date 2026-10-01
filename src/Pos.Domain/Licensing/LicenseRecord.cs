namespace Pos.Domain.Licensing;

/// <summary>Contenido ya validado del archivo de licencia local (012, data-model).</summary>
/// <param name="Version">Versión del formato del archivo.</param>
/// <param name="MachineId">ID de la máquina a la que pertenece la licencia.</param>
/// <param name="FirstRunUtc">Inicio de la evaluación.</param>
/// <param name="LastSeenUtc">Última fecha vista; nunca retrocede, así un reloj atrasado no devuelve días.</param>
/// <param name="TrialDays">Días de evaluación.</param>
/// <param name="Modules">Módulos comprados (solo conocidos).</param>
public sealed record LicenseRecord(
    int Version,
    string MachineId,
    DateTime FirstRunUtc,
    DateTime LastSeenUtc,
    int TrialDays,
    IReadOnlySet<LicensedModule> Modules)
{
    public const int CurrentVersion = 2;

    public const int DefaultTrialDays = 30;
}
