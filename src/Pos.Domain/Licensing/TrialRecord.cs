namespace Pos.Domain.Licensing;

/// <summary>Registro local de la prueba (025, data-model). Ya no lleva módulos: la licencia firmada es la única fuente.</summary>
/// <param name="MachineId">ID de la máquina a la que pertenece el registro.</param>
/// <param name="FirstRunUtc">Inicio de la prueba. Nunca aumenta; si la copia protegida está alterada = <see cref="DateTime.MinValue"/>.</param>
/// <param name="LastSeenUtc">Última fecha vista. Nunca retrocede, así un reloj atrasado no devuelve días.</param>
/// <param name="TrialDays">Días de prueba.</param>
/// <param name="LicenseImportedUtc">
/// Se fija al aceptar la primera licencia y nunca vuelve a nulo; con valor, la prueba ya no aplica (FR-026a).
/// </param>
public sealed record TrialRecord(
    string MachineId,
    DateTime FirstRunUtc,
    DateTime LastSeenUtc,
    int TrialDays,
    DateTime? LicenseImportedUtc)
{
    public const int DefaultTrialDays = 30;
}
