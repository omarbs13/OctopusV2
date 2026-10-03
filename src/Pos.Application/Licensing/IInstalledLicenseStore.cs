namespace Pos.Application.Licensing;

/// <summary>
/// Licencia formato 3 importada, guardada exactamente como se recibió (025, FR-020, research §3). Una
/// sola licencia por instalación; importar reemplaza la anterior.
/// </summary>
public interface IInstalledLicenseStore
{
    /// <summary>Texto exacto del <c>.lic</c> importado; nulo si nunca se importó una.</summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Reemplaza la licencia guardada en una sola transacción.</summary>
    Task ReplaceAsync(string content, DateTime importedAtUtc, CancellationToken cancellationToken);
}
