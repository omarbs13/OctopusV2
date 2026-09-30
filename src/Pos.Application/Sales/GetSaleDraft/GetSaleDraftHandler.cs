using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Products;

namespace Pos.Application.Sales.GetSaleDraft;

/// <summary>
/// Recupera el borrador con los datos vigentes de cada producto y su motivo de no venta, para que
/// <c>Cart.Restore</c> señale las líneas que ya no se pueden vender (US2, escenario 3).
/// </summary>
public sealed partial class GetSaleDraftHandler
{
    private readonly ISaleDraftStore _drafts;
    private readonly IProductRepository _products;
    private readonly ILogger<GetSaleDraftHandler> _logger;

    public GetSaleDraftHandler(ISaleDraftStore drafts, IProductRepository products, ILogger<GetSaleDraftHandler> logger)
    {
        _drafts = drafts;
        _products = products;
        _logger = logger;
    }

    public async Task<Result<RecoveredDraft?>> HandleAsync(CancellationToken cancellationToken)
    {
        var stored = await _drafts.LoadAsync(cancellationToken);
        if (stored is null || stored.Lines.Count == 0)
        {
            return Result.Success<RecoveredDraft?>(null);
        }

        var products = (await _products.GetManyAsync(
                [.. stored.Lines.Select(l => l.ProductId)], includeDeleted: true, cancellationToken))
            .ToDictionary(p => p.Id);

        var lines = new List<RecoveredLineDto>();
        foreach (var line in stored.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                LogMissingProduct(line.ProductId);
                continue;
            }

            var dto = SaleProductMapping.ToDto(product, stock: null);
            lines.Add(new RecoveredLineDto(
                product.Id,
                product.Name,
                product.Sku,
                dto.UnitCode,
                dto.DecimalPlaces,
                product.TracksInventory,
                product.Price.Cents,
                line.QuantityThousandths,
                dto.NotSellableReason));
        }

        return Result.Success<RecoveredDraft?>(lines.Count == 0 ? null : new RecoveredDraft(stored.DraftId, lines));
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "El borrador de venta referencia un producto inexistente y se omitió. ProductId={ProductId}")]
    private partial void LogMissingProduct(Guid productId);
}
