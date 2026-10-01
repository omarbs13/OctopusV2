using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Resultado de verificar un archivo de licencia importable.</summary>
public abstract record LicenseVerification
{
    public sealed record Valid(LicenseGrant Grant) : LicenseVerification;

    public sealed record Rejected(LicenseImportRejection Reason) : LicenseVerification;
}

/// <summary>Verifica sin conexión la firma del proveedor y que la licencia sea de esta máquina (011, research §3).</summary>
public interface ILicenseVerifier
{
    LicenseVerification Verify(string filePath, string machineId);
}
