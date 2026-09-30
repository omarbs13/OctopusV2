using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;

namespace Pos.Application.Sales.SaveSaleDraft;

/// <summary>
/// Guarda el borrador tras cada cambio de la venta (FR-010). Un fallo se registra y no se propaga:
/// nunca debe interrumpir la captura (FR-013).
/// </summary>
public sealed partial class SaveSaleDraftHandler
{
    private readonly ISaleDraftStore _drafts;
    private readonly ILogger<SaveSaleDraftHandler> _logger;

    public SaveSaleDraftHandler(ISaleDraftStore drafts, ILogger<SaveSaleDraftHandler> logger)
    {
        _drafts = drafts;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SaveSaleDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            if (command.Lines.Count == 0)
            {
                await _drafts.DiscardAsync(cancellationToken);
            }
            else
            {
                await _drafts.SaveAsync(command.DraftId, command.Lines, cancellationToken);
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
