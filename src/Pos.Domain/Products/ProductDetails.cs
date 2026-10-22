namespace Pos.Domain.Products;

/// <summary>
/// Datos del producto que vienen del proveedor (026). Los precios van en centavos y el embalaje en
/// milésimas de metro cuadrado por caja.
/// </summary>
public sealed record ProductDetails(
    string Model,
    string? Measures = null,
    string? Brand = null,
    Guid? SupplierId = null,
    long? PackagingThousandths = null,
    int? PiecesPerBox = null,
    long? PricePerSqmCents = null,
    long? PricePerBoxCents = null,
    long? PiecePriceCents = null);
