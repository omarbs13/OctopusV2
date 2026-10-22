namespace Pos.Application.Products;

/// <summary>
/// Datos capturados de un producto, comunes al alta y a la edición. Los importes, el embalaje y las
/// piezas llegan como texto para poder señalar el campo con error (026).
/// </summary>
public interface IProductInput
{
    string Model { get; }

    string? Sku { get; }

    string? Barcode { get; }

    string? Measures { get; }

    string? Brand { get; }

    Guid? SupplierId { get; }

    /// <summary>Metros cuadrados por caja; vacío o "NA" = no aplica.</summary>
    string? PackagingText { get; }

    string? PiecesPerBoxText { get; }

    string? PricePerSqmText { get; }

    string? PricePerBoxText { get; }

    string? PiecePriceText { get; }

    string UnitCode { get; }

    bool TracksInventory { get; }

    string? MinimumStockText { get; }

    string? ReorderPointText { get; }
}
