using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Printing.ListPrinters;

/// <summary>Impresoras del sistema; si el catálogo falla devuelve una lista vacía.</summary>
public sealed partial class ListPrintersHandler
{
    private readonly IAccessControl _access;
    private readonly IPrinterCatalog _catalog;
    private readonly ILogger<ListPrintersHandler> _logger;

    public ListPrintersHandler(IAccessControl access, IPrinterCatalog catalog, ILogger<ListPrintersHandler> logger)
    {
        _access = access;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<string>>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageSettings, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<string>>(access.Error!);
        }

        try
        {
            return Result.Success(await _catalog.ListAsync(cancellationToken));
        }
#pragma warning disable CA1031 // Un catálogo que falla equivale a "sin impresoras".
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(ex);
            return Result.Success<IReadOnlyList<string>>([]);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo listar las impresoras del sistema")]
    private partial void LogFailed(Exception exception);
}
