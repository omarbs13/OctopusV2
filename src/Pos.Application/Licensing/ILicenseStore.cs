using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>Resultado de leer el archivo de licencia local.</summary>
public abstract record LicenseLoadResult
{
    public sealed record Loaded(LicenseRecord Record) : LicenseLoadResult;

    public sealed record Missing : LicenseLoadResult;

    public sealed record Invalid(InvalidLicenseReason Reason) : LicenseLoadResult;
}

/// <summary>Archivo de licencia local. Nunca lanza por contenido inválido; sí por fallas de E/S al guardar.</summary>
public interface ILicenseStore
{
    LicenseLoadResult Load();

    void Save(LicenseRecord record);
}
