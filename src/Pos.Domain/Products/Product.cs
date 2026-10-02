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

    /// <summary>Indica si el producto controla inventario (FR-001); por omisión no.</summary>
    public bool TracksInventory { get; private set; }

    /// <summary>Existencia mínima para alertas (FR-004). Nulo si no controla inventario o no la define.</summary>
    public long? MinimumStockThousandths { get; private set; }

    public Quantity? MinimumStock => MinimumStockThousandths is { } m ? Quantity.FromThousandths(m) : null;

    /// <summary>Marca de producto crítico: aparece en las alertas de Inicio cuando su existencia es baja (009).</summary>
    public bool IsCritical { get; private set; }

    /// <summary>
    /// Categoría del producto, o nula ("Sin categoría", 016). Sin clave foránea: que esté activa al
    /// asignarla lo valida el caso de uso (FR-011).
    /// </summary>
    public Guid? CategoryId { get; private set; }

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

    public static Product Create(
        string name,
        string sku,
        string? barcode,
        Money price,
        string unitCode,
        bool tracksInventory = false,
        Quantity? minimumStock = null,
        Guid? categoryId = null)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
            CategoryId = categoryId,
        };
        product.Apply(name, sku, barcode, price, unitCode);
        product.ApplyInventory(tracksInventory, minimumStock);
        return product;
    }

    /// <summary>
    /// Actualiza el producto. Con <paramref name="hasMovements"/> no se puede cambiar la unidad ni
    /// dejar de controlar inventario (FR-006).
    /// </summary>
    public void Update(
        string name,
        string sku,
        string? barcode,
        Money price,
        string unitCode,
        bool isActive,
        bool tracksInventory = false,
        Quantity? minimumStock = null,
        bool hasMovements = false,
        Guid? categoryId = null)
    {
        if (!CanChangeInventorySettings(hasMovements, UnitCode, unitCode, TracksInventory, tracksInventory))
        {
            throw new DomainException(
                "Un producto con movimientos de inventario no puede cambiar de unidad ni dejar de controlar inventario.");
        }

        Apply(name, sku, barcode, price, unitCode);
        ApplyInventory(tracksInventory, minimumStock);
        IsActive = isActive;
        CategoryId = categoryId;
    }

    /// <summary>Marca o desmarca el producto como crítico; no exige movimientos ni cambia otros campos.</summary>
    public void MarkCritical(bool isCritical) => IsCritical = isCritical;

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

    /// <summary>
    /// La existencia mínima, si existe, debe respetar los decimales de la unidad (FR-003, FR-004).
    /// </summary>
    public static bool IsValidMinimumStock(Quantity? minimum, UnitOfMeasure unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return minimum is null
            || (minimum.Value <= Quantity.FromThousandths(Quantity.MaxCaptureThousandths) && minimum.Value.FitsDecimals(unit.DecimalPlaces));
    }

    /// <summary>
    /// Con movimientos no se puede cambiar de unidad ni pasar de controlar a no controlar inventario (FR-006).
    /// </summary>
    public static bool CanChangeInventorySettings(
        bool hasMovements,
        string currentUnit,
        string newUnit,
        bool currentTracks,
        bool newTracks) =>
        !hasMovements || (string.Equals(currentUnit, newUnit, StringComparison.Ordinal) && (newTracks || !currentTracks));

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

    private void ApplyInventory(bool tracksInventory, Quantity? minimumStock)
    {
        var unit = UnitOfMeasure.Find(UnitCode)!;
        if (tracksInventory && !IsValidMinimumStock(minimumStock, unit))
        {
            throw new DomainException("La existencia mínima no es válida para la unidad del producto.");
        }

        TracksInventory = tracksInventory;
        MinimumStockThousandths = tracksInventory ? minimumStock?.Thousandths : null;
    }

    [GeneratedRegex(@"^[0-9]{8,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex BarcodePattern();
}
