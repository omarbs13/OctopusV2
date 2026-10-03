using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Resultado de leer el archivo de prueba local (<c>license.lic</c>).</summary>
public abstract record LicenseLoadResult
{
    /// <param name="Record">Registro de la prueba.</param>
    /// <param name="HadLegacyModules">
    /// Era un archivo de prueba v2 con módulos de 011/012: había una licencia que ya no se reconoce (025, FR-021).
    /// </param>
    public sealed record Loaded(TrialRecord Record, bool HadLegacyModules = false) : LicenseLoadResult;

    public sealed record Missing : LicenseLoadResult;

    /// <summary>Corrupto, alterado, de otra máquina o de una versión que ya no se lee.</summary>
    public sealed record Unusable : LicenseLoadResult;
}

/// <summary>Archivo de prueba local. Nunca lanza por contenido inválido; sí por fallas de E/S al guardar.</summary>
public interface ILicenseStore
{
    LicenseLoadResult Load();

    void Save(TrialRecord record);
}
