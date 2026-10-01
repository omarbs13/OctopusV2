using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Licencia 011 (versión 1 del archivo); solo sirve para migrar (012, FR-022).</summary>
/// <param name="FirstRunUtc">Inicio de la evaluación.</param>
/// <param name="LastSeenUtc">Última fecha vista.</param>
/// <param name="HasGrant">Tenía una concesión del proveedor.</param>
/// <param name="ValidUntil">Fin de la concesión; nulo = sin vencimiento.</param>
public sealed record LegacyLicense(DateTime FirstRunUtc, DateTime LastSeenUtc, bool HasGrant, DateOnly? ValidUntil);

/// <summary>Resultado de leer el archivo de licencia local.</summary>
public abstract record LicenseLoadResult
{
    public sealed record Loaded(LicenseRecord Record) : LicenseLoadResult;

    public sealed record Missing : LicenseLoadResult;

    /// <summary>Corrupto, alterado o de otra máquina.</summary>
    public sealed record Unusable : LicenseLoadResult;

    public sealed record LegacyV1(LegacyLicense License) : LicenseLoadResult;
}

/// <summary>Archivo de licencia local. Nunca lanza por contenido inválido; sí por fallas de E/S al guardar.</summary>
public interface ILicenseStore
{
    LicenseLoadResult Load();

    void Save(LicenseRecord record);
}
