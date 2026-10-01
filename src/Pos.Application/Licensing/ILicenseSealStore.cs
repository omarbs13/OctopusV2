namespace Pos.Application.Licensing;

/// <summary>Copia protegida de la fecha de inicio y la última fecha vista (012, FR-018).</summary>
public sealed record LicenseSeal(DateTime FirstRunUtc, DateTime LastSeenUtc);

public interface ILicenseSealStore
{
    /// <summary>Nulo si no existe o la carga está alterada o ilegible.</summary>
    Task<LicenseSeal?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(LicenseSeal seal, CancellationToken cancellationToken);
}
