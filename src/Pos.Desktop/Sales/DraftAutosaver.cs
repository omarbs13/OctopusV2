using Pos.Application.Sales;
using Serilog;

namespace Pos.Desktop.Sales;

/// <summary>
/// Guarda el borrador de la venta tras cada cambio, sin retraso (research §7, SC-004). Serializa las
/// escrituras: una a la vez y siempre la más reciente, así los escaneos rápidos no saturan la base.
/// Nunca lanza: un error se registra y la captura continúa (FR-013).
/// </summary>
public sealed class DraftAutosaver
{
    private readonly Func<Guid, IReadOnlyList<DraftLineDto>, DraftOrderDiscountDto?, Task> _save;
    private readonly ILogger _logger;
    private readonly object _gate = new();

    private (Guid DraftId, IReadOnlyList<DraftLineDto> Lines, DraftOrderDiscountDto? Order)? _pending;
    private Task _running = Task.CompletedTask;
    private bool _draining;

    public DraftAutosaver(Func<Guid, IReadOnlyList<DraftLineDto>, DraftOrderDiscountDto?, Task> save, ILogger logger)
    {
        _save = save;
        _logger = logger;
    }

    /// <summary>Pide guardar el estado actual con sus descuentos (015); sin líneas equivale a descartar el borrador.</summary>
    public void Save(Guid draftId, IReadOnlyList<DraftLineDto> lines, DraftOrderDiscountDto? order = null)
    {
        lock (_gate)
        {
            _pending = (draftId, lines, order);
            if (_draining)
            {
                return;
            }

            _draining = true;
            _running = Task.Run(DrainAsync);
        }
    }

    /// <summary>Espera a que termine la escritura pendiente, si la hay.</summary>
    public async Task FlushAsync()
    {
        while (true)
        {
            Task running;
            lock (_gate)
            {
                if (!_draining)
                {
                    return;
                }

                running = _running;
            }

            await running;
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            (Guid DraftId, IReadOnlyList<DraftLineDto> Lines, DraftOrderDiscountDto? Order) item;
            lock (_gate)
            {
                if (_pending is not { } next)
                {
                    _draining = false;
                    return;
                }

                item = next;
                _pending = null;
            }

            try
            {
                await _save(item.DraftId, item.Lines, item.Order);
            }
#pragma warning disable CA1031 // El borrador es de apoyo: una falla no debe interrumpir la venta.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.Warning(ex, "No se pudo guardar el borrador de la venta. DraftId={DraftId} Lines={Lines}", item.DraftId, item.Lines.Count);
            }
        }
    }
}
