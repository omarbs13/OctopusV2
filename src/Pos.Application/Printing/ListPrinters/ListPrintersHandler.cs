using Microsoft.Extensions.Logging;

namespace Pos.Application.Printing.ListPrinters;

/// <summary>Impresoras del sistema; si el catálogo falla devuelve una lista vacía.</summary>
public sealed partial class ListPrintersHandler
{
    private readonly IPrinterCatalog _catalog;
    private readonly ILogger<ListPrintersHandler> _logger;

    public ListPrintersHandler(IPrinterCatalog catalog, ILogger<ListPrintersHandler> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _catalog.ListAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Un catálogo que falla equivale a "sin impresoras".
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo listar las impresoras del sistema")]
    private partial void LogFailed(Exception exception);
}
