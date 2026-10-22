using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Mensajes de validación de productos, en español.</summary>
public static class ProductMessages
{
    public const string ModelRequired = "El modelo es obligatorio.";
    public const string SkuRequired = "El SKU es obligatorio.";
    public const string PackagingInvalid = "Capture el embalaje en metros cuadrados por caja, hasta 3 decimales, o NA.";
    public const string PackagingNotPositive = "El embalaje debe ser mayor que 0 (o NA si no aplica).";
    public const string PiecesPerBoxInvalid = "Las piezas por caja deben ser un número entero mayor que 0.";
    public const string TilePriceRequired = "Capture el precio por metro cuadrado o el precio por caja.";
    public const string SupplierNotAssignable = "El proveedor no existe o no está activo.";
    public const string PriceDifference =
        "El precio por caja difiere en más de 2 % de precio por m² × embalaje. Revise los valores.";
    public const string SkuWithSpaces = "El SKU no puede contener espacios.";
    public const string PriceRequired = "El precio es obligatorio.";
    public const string PriceFormat = "Capture el precio como 1234.50 o 1,234.50.";
    public const string PriceNotPositive = "El precio debe ser mayor que 0.";
    public const string PriceTooManyDecimals = "El precio admite máximo 2 decimales.";
    public const string PriceTooLarge = "El precio máximo es $999,999.99.";
    public const string UnitRequired = "La unidad de medida es obligatoria.";
    public const string ReorderPointNotBelowMinimum = "El punto de reorden debe ser menor que la existencia mínima.";
    public const string ImageTooLarge = "La imagen pesa más de 5 MB. Elija un archivo más pequeño.";
    public const string ImageUnsupportedFormat = "Formato no admitido. Use una imagen JPG, PNG o WEBP.";
    public const string ImageCorrupt = "No se pudo leer la imagen; el archivo está dañado o no es una imagen válida.";
    public const string ImageDimensionsTooLarge = "La imagen tiene dimensiones demasiado grandes para procesarse.";

    public static readonly string ModelTooLong = $"El modelo admite hasta {Product.ModelCaptureMaxLength} caracteres.";
    public static readonly string MeasuresTooLong = $"Las medidas admiten hasta {Product.MeasuresMaxLength} caracteres.";
    public static readonly string BrandTooLong = $"La marca admite hasta {Product.BrandMaxLength} caracteres.";
    public static readonly string SkuTooLong = $"El SKU admite hasta {Product.SkuMaxLength} caracteres.";
    public static readonly string BarcodeFormat =
        $"El código de barras admite hasta {Product.BarcodeMaxLength} letras, dígitos, espacios interiores y los símbolos - . $ / + %.";
}
