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

    /// <summary>Tamaño máximo del archivo de imagen que se acepta (5 MB).</summary>
    public const long ImageMaxBytes = 5 * 1024 * 1024;

    /// <summary>Lado mayor de la imagen optimizada (003, clarificación 3).</summary>
    public const int ImageMaxSide = 800;

    /// <summary>Lado mayor de la miniatura del listado.</summary>
    public const int ThumbnailMaxSide = 128;

    private Product()
    {
        Name = string.Empty;
        NameSearch = string.Empty;
        Sku = string.Empty;
        UnitCode = UnitOfMeasure.Default.Code;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NameSearch { get; private set; }

    public string Sku { get; private set; }

    public string? Barcode { get; private set; }

    public Money Price { get; private set; }

    /// <summary>Clave de la unidad de medida (<see cref="UnitOfMeasure"/>).</summary>
    public string UnitCode { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>
    /// Imagen del producto, o nula. Solo está cargada cuando el repositorio la incluye
    /// explícitamente; sin cargar, nulo no significa "sin imagen".
    /// </summary>
    public ProductImage? Image { get; private set; }

    /// <summary>
    /// Indica que la imagen cambió desde que se cargó el producto, para que la persistencia marque
    /// el producto como modificado (versión y fecha) en la misma transacción.
    /// </summary>
    public bool ImageChanged { get; private set; }

    public static Product Create(string name, string sku, string? barcode, Money price, string unitCode)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
        };
        product.Apply(name, sku, barcode, price, unitCode);
        return product;
    }

    public void Update(string name, string sku, string? barcode, Money price, string unitCode, bool isActive)
    {
        Apply(name, sku, barcode, price, unitCode);
        IsActive = isActive;
    }

    /// <summary>Asigna o reemplaza la imagen (ya optimizada) del producto.</summary>
    public void SetImage(byte[] content, byte[] thumbnail, int width, int height)
    {
        if (Image is null)
        {
            Image = ProductImage.Create(Id, content, thumbnail, width, height);
        }
        else
        {
            Image.Replace(content, thumbnail, width, height);
        }

        ImageChanged = true;
    }

    /// <summary>Quita la imagen. La imagen debe estar cargada; sin imagen no hace nada.</summary>
    public void RemoveImage()
    {
        if (Image is null)
        {
            return;
        }

        Image = null;
        ImageChanged = true;
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

    /// <summary>El precio de venta debe ser mayor que 0 (003, FR-015).</summary>
    public static bool IsValidPrice(Money price) => price.Cents > 0;

    /// <summary>Indica si un texto de búsqueda tiene la forma de un código de barras completo.</summary>
    public static bool LooksLikeFullBarcode(string text) => BarcodePattern().IsMatch(text);

    private void Apply(string name, string sku, string? barcode, Money price, string unitCode)
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

        if (!IsValidPrice(price))
        {
            throw new DomainException("El precio debe ser mayor que 0.");
        }

        if (!UnitOfMeasure.IsValidCode(unitCode))
        {
            throw new DomainException("La unidad de medida no es válida.");
        }

        Name = normalizedName;
        NameSearch = TextNormalizer.ForSearch(normalizedName);
        Sku = normalizedSku;
        Barcode = normalizedBarcode;
        Price = price;
        UnitCode = unitCode;
    }

    [GeneratedRegex(@"^[0-9]{8,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex BarcodePattern();
}
