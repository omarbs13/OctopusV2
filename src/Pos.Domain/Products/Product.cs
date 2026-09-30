using System.Text.RegularExpressions;
using Pos.Domain.Common;

namespace Pos.Domain.Products;

/// <summary>
/// Artículo del catálogo. Los campos de auditoría los asigna la persistencia
/// a partir del reloj y del usuario actual.
/// </summary>
public sealed partial class Product
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;
    public const int BarcodeMinLength = 8;
    public const int BarcodeMaxLength = 14;

    private Product()
    {
        Name = string.Empty;
        NameSearch = string.Empty;
        Sku = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NameSearch { get; private set; }

    public string Sku { get; private set; }

    public string? Barcode { get; private set; }

    public Money Price { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Product Create(string name, string sku, string? barcode, Money price)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        product.Apply(name, sku, barcode, price);
        return product;
    }

    public void Update(string name, string sku, string? barcode, Money price, bool isActive)
    {
        Apply(name, sku, barcode, price);
        IsActive = isActive;
    }

    public void Delete(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("La fecha de borrado debe estar en UTC.");
        }

        DeletedAt ??= utcNow;
    }

    /// <summary>Recorta el nombre. Nulo o vacío se trata como vacío.</summary>
    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>Recorta los extremos y pasa a mayúsculas invariantes.</summary>
    public static string NormalizeSku(string? sku) => (sku ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Recorta; vacío o solo espacios se convierte en nulo.</summary>
    public static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is > 0 and <= NameMaxLength;

    public static bool IsValidSku(string normalizedSku) =>
        normalizedSku.Length is > 0 and <= SkuMaxLength && !normalizedSku.Any(char.IsWhiteSpace);

    public static bool IsValidBarcode(string? normalizedBarcode) =>
        normalizedBarcode is null || BarcodePattern().IsMatch(normalizedBarcode);

    /// <summary>Indica si un texto de búsqueda tiene la forma de un código de barras completo.</summary>
    public static bool LooksLikeFullBarcode(string text) => BarcodePattern().IsMatch(text);

    private void Apply(string name, string sku, string? barcode, Money price)
    {
        var normalizedName = NormalizeName(name);
        var normalizedSku = NormalizeSku(sku);
        var normalizedBarcode = NormalizeBarcode(barcode);

        if (!IsValidName(normalizedName))
        {
            throw new DomainException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        }

        if (!IsValidSku(normalizedSku))
        {
            throw new DomainException($"El SKU es obligatorio, sin espacios y hasta {SkuMaxLength} caracteres.");
        }

        if (!IsValidBarcode(normalizedBarcode))
        {
            throw new DomainException(
                $"El código de barras debe tener solo dígitos, entre {BarcodeMinLength} y {BarcodeMaxLength}.");
        }

        Name = normalizedName;
        NameSearch = TextNormalizer.ForSearch(normalizedName);
        Sku = normalizedSku;
        Barcode = normalizedBarcode;
        Price = price;
    }

    [GeneratedRegex(@"^[0-9]{8,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex BarcodePattern();
}
