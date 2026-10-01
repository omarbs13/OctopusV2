namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Dato técnico de la instalación (012): copia cifrada de la fecha de inicio y la última fecha vista.
/// No es una entidad de negocio, así que no tiene auditoría, borrado lógico ni versión.
/// </summary>
public sealed class LicenseSealEntity
{
    public Guid Id { get; set; }

    public byte[] Payload { get; set; } = [];
}
