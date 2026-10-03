using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.SaveSaleDraft;

/// <summary>
/// Guarda el borrador tras cada cambio de la venta (FR-010). Un fallo se registra y no se propaga:
/// nunca debe interrumpir la captura (FR-013). En bloqueo solo se sigue o se descarta la venta en curso
/// (025, FR-030a); un borrador nuevo es una venta nueva y se rechaza.
/// </summary>
public sealed partial class SaveSaleDraftHandler
{
    private readonly IAccessControl _access;
    private readonly ISaleDraftStore _drafts;
    private readonly ILogger<SaveSaleDraftHandler> _logger;
    private readonly ILicenseState? _license;

    public SaveSaleDraftHandler(IAccessControl access, ISaleDraftStore drafts, ILogger<SaveSaleDraftHandler> logger, ILicenseState? license = null)
    {
        _license = license;
        _access = access;
        _drafts = drafts;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SaveSaleDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        if (LicenseGate.WhenBlocked(_license) is not null
            && DraftInProgress.RejectNewSaleWhenBlocked(_license, await _drafts.LoadAsync(cancellationToken), command.DraftId) is { } blocked)
        {
            return Result.Failure(blocked);
        }

        try
        {
            if (command.Lines.Count == 0)
            {
                await _drafts.DiscardAsync(cancellationToken);
            }
            else
            {
                await _drafts.SaveAsync(command.DraftId, command.Lines, command.OrderDiscount, cancellationToken);
            }

            return Result.Success();
        }
#pragma warning disable CA1031 // El borrador es de apoyo: una falla no debe interrumpir la venta.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSaveFailed(ex, command.DraftId, command.Lines.Count);
            return Result.Success();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No se pudo guardar el borrador de la venta. DraftId={DraftId} Lines={Lines}")]
    private partial void LogSaveFailed(Exception exception, Guid draftId, int lines);
}
