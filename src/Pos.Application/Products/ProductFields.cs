namespace Pos.Application.Products;

/// <summary>Nombres de campo de los errores; coinciden con los controles de la interfaz.</summary>
public static class ProductFields
{
    public const string Model = "Model";
    public const string Measures = "Measures";
    public const string Brand = "Brand";
    public const string Supplier = "Supplier";
    public const string Packaging = "Packaging";
    public const string PiecesPerBox = "PiecesPerBox";
    public const string PricePerSqm = "PricePerSqm";
    public const string PricePerBox = "PricePerBox";
    public const string Sku = "Sku";
    public const string Barcode = "Barcode";

    /// <summary>Precio por pieza (el precio de los productos que no son de loseta).</summary>
    public const string Price = "Price";
    public const string UnitCode = "UnitCode";
    public const string TracksInventory = "TracksInventory";
    public const string MinimumStock = "MinimumStock";
    public const string ReorderPoint = "ReorderPoint";

    /// <summary>Identidad proveedor + modelo + medidas (026).</summary>
    public const string Identity = "Identity";
}
