namespace Pos.Application.Licensing;

/// <summary>Copia protegida de las fechas de la prueba (012, FR-018; 025, FR-026a).</summary>
public sealed record LicenseSeal(DateTime FirstRunUtc, DateTime LastSeenUtc, DateTime? LicenseImportedUtc = null);

/// <summary>Resultado de leer la copia protegida (025, research §4).</summary>
public abstract record LicenseSealReadResult
{
    public sealed record Missing : LicenseSealReadResult;

    /// <summary>La fila existe pero no se puede descifrar o validar: la prueba se considera vencida.</summary>
    public sealed record Tampered : LicenseSealReadResult;

    public sealed record Valid(LicenseSeal Seal) : LicenseSealReadResult;
}

public interface ILicenseSealStore
{
    Task<LicenseSealReadResult> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(LicenseSeal seal, CancellationToken cancellationToken);
}
