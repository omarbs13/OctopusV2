using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Resultado de verificar el texto de una licencia formato 3.</summary>
public abstract record LicenseVerification
{
    public sealed record Valid(SignedLicense License) : LicenseVerification;

    public sealed record Rejected(LicenseImportRejection Reason) : LicenseVerification;
}

/// <summary>
/// Verifica sin conexión una licencia formato 3 (025, contracts/license-format.md §5, pasos 1 a 6): sobre,
/// formato, firma del proveedor, contenido e ID de máquina. Trabaja sobre el texto para poder
/// reverificar la licencia guardada en cada arranque. El paso 7 (antigüedad) lo aplica quien importa.
/// </summary>
public interface ILicenseVerifier
{
    /// <summary>Tamaño máximo del archivo <c>.lic</c> (contrato §2).</summary>
    public const int MaxBytes = 64 * 1024;

    LicenseVerification Verify(string content, string machineId);
}
