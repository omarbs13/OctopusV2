using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.GetSaleDraft;

/// <summary>
/// Recupera el borrador con los datos vigentes de cada producto y su motivo de no venta, para que
/// <c>Cart.Restore</c> señale las líneas que ya no se pueden vender (US2, escenario 3).
/// </summary>
public sealed partial class GetSaleDraftHandler
{
    private readonly IAccessControl _access;
    private readonly ISaleDraftStore _drafts;
    private readonly IProductRepository _products;
    private readonly ILogger<GetSaleDraftHandler> _logger;

    public GetSaleDraftHandler(IAccessControl access, ISaleDraftStore drafts, IProductRepository products, ILogger<GetSaleDraftHandler> logger)
    {
        _access = access;
        _drafts = drafts;
        _products = products;
        _logger = logger;
    }

    public async Task<Result<RecoveredDraft?>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<RecoveredDraft?>(access.Error!);
        }

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
