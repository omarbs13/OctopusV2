using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Mensajes de validación de productos, en español.</summary>
public static class ProductMessages
{
    public const string NameRequired = "El nombre es obligatorio.";
    public const string SkuRequired = "El SKU es obligatorio.";
    public const string SkuWithSpaces = "El SKU no puede contener espacios.";
    public const string PriceRequired = "El precio es obligatorio.";
    public const string PriceFormat = "Capture el precio como 1234.50 o 1,234.50.";
    public const string PriceNotPositive = "El precio debe ser mayor que 0.";
    public const string PriceTooManyDecimals = "El precio admite máximo 2 decimales.";
    public const string PriceTooLarge = "El precio máximo es $999,999.99.";
    public const string UnitRequired = "La unidad de medida es obligatoria.";
    public const string ImageTooLarge = "La imagen pesa más de 5 MB. Elija un archivo más pequeño.";
    public const string ImageUnsupportedFormat = "Formato no admitido. Use una imagen JPG, PNG o WEBP.";
    public const string ImageCorrupt = "No se pudo leer la imagen; el archivo está dañado o no es una imagen válida.";
    public const string ImageDimensionsTooLarge = "La imagen tiene dimensiones demasiado grandes para procesarse.";

    public static readonly string NameTooLong = $"El nombre admite hasta {Product.NameMaxLength} caracteres.";
    public static readonly string SkuTooLong = $"El SKU admite hasta {Product.SkuMaxLength} caracteres.";
    public static readonly string BarcodeFormat =
        $"El código de barras debe tener solo dígitos, entre {Product.BarcodeMinLength} y {Product.BarcodeMaxLength}.";
}
