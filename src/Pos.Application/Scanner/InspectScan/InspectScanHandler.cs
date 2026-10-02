using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Users.Session;
using Pos.Domain.Products;
using Pos.Domain.Users;

namespace Pos.Application.Scanner.InspectScan;

/// <summary>
/// Diagnóstico de una lectura del escáner (021, research §10): formato, largo y producto con ese código de
/// barras. Solo exige una sesión iniciada, sin permiso de rol (FR-015): el nombre, el SKU y el estado de un
/// producto los ve cualquier cajero al vender. No escribe datos ni bitácora (FR-019).
/// </summary>
public sealed class InspectScanHandler
{
    private readonly IUserSession _session;
    private readonly IProductRepository _products;

    public InspectScanHandler(IUserSession session, IProductRepository products)
    {
        _session = session;
        _products = products;
    }

    public async Task<Result<ScanInspectionDto>> HandleAsync(InspectScanQuery query, CancellationToken cancellationToken)
    {
        if (_session.User is null)
        {
            // Sin sesión no hay rol; se informa con el permiso básico de todos los roles.
            return Result.Failure<ScanInspectionDto>(new Forbidden(Permission.Sell, CanBeAuthorized: false));
        }

        ArgumentNullException.ThrowIfNull(query);

        var raw = query.RawText ?? string.Empty;
        var format = Barcode.Classify(raw);
        var normalized = Barcode.Normalize(raw);
        var product = normalized is not null && Barcode.IsValidForCatalog(normalized)
            ? await _products.FindByBarcodeAsync(normalized, cancellationToken)
            : null;

        return Result.Success(new ScanInspectionDto(
            normalized ?? string.Empty,
            format,
            raw.Length,
            product is null ? null : new ScanProductDto(product.Name, product.Sku, product.IsActive)));
    }
}
