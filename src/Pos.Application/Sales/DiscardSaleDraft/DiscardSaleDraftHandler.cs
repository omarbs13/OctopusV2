using Pos.Application.Abstractions;

namespace Pos.Application.Sales.DiscardSaleDraft;

/// <summary>Borra el borrador de la venta en curso.</summary>
public sealed class DiscardSaleDraftHandler
{
    private readonly ISaleDraftStore _drafts;

    public DiscardSaleDraftHandler(ISaleDraftStore drafts) => _drafts = drafts;

    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        await _drafts.DiscardAsync(cancellationToken);
        return Result.Success();
    }
}
