using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Módulos firmados por el proveedor (solo los conocidos).</summary>
public sealed record ExtendedGrant(IReadOnlySet<LicensedModule> Modules, DateTime IssuedUtc);

/// <summary>Resultado de verificar un archivo de licencia importable.</summary>
public abstract record LicenseVerification
{
    public sealed record Valid(ExtendedGrant Grant) : LicenseVerification;

    public sealed record Rejected(LicenseImportRejection Reason) : LicenseVerification;
}

/// <summary>Verifica sin conexión la firma del proveedor y que la licencia sea de esta máquina (012, research §5).</summary>
public interface ILicenseVerifier
{
    LicenseVerification Verify(string filePath, string machineId);
}
