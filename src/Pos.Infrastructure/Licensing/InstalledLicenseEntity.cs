namespace Pos.Infrastructure.Licensing;

/// <summary>
/// Dato técnico de la instalación (025, research §3): la licencia formato 3 importada, una sola fila. Como
/// <see cref="LicenseSealEntity"/>, no es una entidad de negocio y no tiene auditoría, borrado lógico ni versión.
/// </summary>
public sealed class InstalledLicenseEntity
{
    /// <summary>GUID v7; una sola fila.</summary>
    public Guid Id { get; set; }

    /// <summary>Texto exacto del <c>.lic</c> importado (≤ 64 KiB).</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }
}
