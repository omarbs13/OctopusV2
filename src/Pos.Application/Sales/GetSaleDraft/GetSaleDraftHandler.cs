using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Sales.GetSaleDraft;

/// <summary>
/// Recupera el borrador con los datos vigentes de cada producto y su motivo de no venta, para que
/// <c>Cart.Restore</c> señale las líneas que ya no se pueden vender (US2, escenario 3). Conserva los
/// descuentos y la aprobación de cada uno (015, research §11); con el módulo Descuentos inactivo los
/// descarta y lo indica con <see cref="RecoveredDraft.DiscountsDropped"/>.
/// </summary>
public sealed partial class GetSaleDraftHandler
{
    private readonly IAccessControl _access;
    private readonly ISaleDraftStore _drafts;
    private readonly IProductRepository _products;
    private readonly ILogger<GetSaleDraftHandler> _logger;
    private readonly ILicenseState? _license;

    public GetSaleDraftHandler(
        IAccessControl access,
        ISaleDraftStore drafts,
        IProductRepository products,
        ILogger<GetSaleDraftHandler> logger,
        ILicenseState? license = null)
    {
        _license = license;
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

        var discountsActive = _license?.IsModuleActive(LicensedModule.Discounts) != false;
        var hadDiscounts = stored.OrderDiscount is not null || stored.Lines.Any(l => l.Discount is not null);
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
                dto.NotSellableReason,
                discountsActive ? line.Discount : null));
        }

        if (lines.Count == 0)
        {
            return Result.Success<RecoveredDraft?>(null);
        }

        var dropped = hadDiscounts && !discountsActive;
        if (dropped)
        {
            LogDiscountsDropped(stored.DraftId);
        }

        return Result.Success<RecoveredDraft?>(new RecoveredDraft(
            stored.DraftId,
            lines,
            discountsActive ? stored.OrderDiscount : null,
            dropped));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Venta conservada recuperada sin descuentos: el módulo Descuentos no está activo. DraftId={DraftId}")]
    private partial void LogDiscountsDropped(Guid draftId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "El borrador de venta referencia un producto inexistente y se omitió. ProductId={ProductId}")]
    private partial void LogMissingProduct(Guid productId);
}
