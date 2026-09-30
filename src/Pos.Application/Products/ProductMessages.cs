using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>Mensajes de validación de productos, en español.</summary>
public static class ProductMessages
{
    public const string NameRequired = "El nombre es obligatorio.";
    public const string SkuRequired = "El SKU es obligatorio.";
    public const string SkuWithSpaces = "El SKU no puede contener espacios.";
    public const string PriceRequired = "El precio es obligatorio.";
    public const string PriceFormat =
        "Capture el precio con dígitos y punto decimal, por ejemplo 1234.50 (máximo 999999.99 y 2 decimales).";

    public static readonly string NameTooLong = $"El nombre admite hasta {Product.NameMaxLength} caracteres.";
    public static readonly string SkuTooLong = $"El SKU admite hasta {Product.SkuMaxLength} caracteres.";
    public static readonly string BarcodeFormat =
        $"El código de barras debe tener solo dígitos, entre {Product.BarcodeMinLength} y {Product.BarcodeMaxLength}.";
}
