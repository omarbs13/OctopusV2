namespace Pos.Application.Printing;

/// <summary>Impresoras instaladas en el sistema operativo.</summary>
public interface IPrinterCatalog
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
}
