using System.Text.RegularExpressions;
using Pos.Domain.Common;
using Pos.Domain.Sales;
using BarcodeRules = Pos.Domain.Products.Barcode;

namespace Pos.Domain.Products;

/// <summary>
/// Artículo del catálogo. Los campos de auditoría los asigna la persistencia
/// a partir del reloj y del usuario actual.
/// </summary>
public sealed partial class Product
{
    public const int NameMaxLength = 200;

    /// <summary>Longitud máxima del modelo al capturar (026); los modelos migrados pueden llegar a <see cref="NameMaxLength"/>.</summary>
    public const int ModelCaptureMaxLength = 150;

    public const int MeasuresMaxLength = 49;
    public const int BrandMaxLength = 100;
    public const int SkuMaxLength = 50;
    public const int BarcodeMinLength = 1;
    public const int BarcodeMaxLength = BarcodeRules.MaxLength;

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
        Model = string.Empty;
        ModelKey = string.Empty;
        Sku = string.Empty;
        UnitCode = UnitOfMeasure.Default.Code;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NameSearch { get; private set; }

    public string Sku { get; private set; }

    public string? Barcode { get; private set; }

    /// <summary>Modelo del proveedor (026). El nombre se deriva de él y de las medidas.</summary>
    public string Model { get; private set; }

    public string ModelKey { get; private set; }

    public string? Measures { get; private set; }

    public string? MeasuresKey { get; private set; }

    public string? Brand { get; private set; }

    public string? BrandSearch { get; private set; }

    /// <summary>Proveedor del producto, o nulo. Sin clave foránea: que esté activo al asignarlo lo valida el caso de uso.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Embalaje: metros cuadrados por caja, en milésimas. Nulo si no aplica.</summary>
    public long? PackagingThousandths { get; private set; }

    public int? PiecesPerBox { get; private set; }

    /// <summary>Precio por metro cuadrado (X M2), en centavos.</summary>
    public long? PricePerSqmCents { get; private set; }

    /// <summary>Precio por caja (X CAJA), en centavos.</summary>
    public long? PricePerBoxCents { get; private set; }

    /// <summary>Precio por pieza, en centavos. Obligatorio si el producto no es de loseta.</summary>
    public long? PiecePriceCents { get; private set; }

    /// <summary>Proveedor + modelo + medidas normalizados. Nulo solo en duplicados migrados.</summary>
    public string? IdentityKey { get; private set; }

    /// <summary>Marcado "Para revisión" por la migración (duplicado, sin modelo válido o loseta sin embalaje).</summary>
    public bool NeedsReview { get; private set; }

    /// <summary>
    /// Precio con el que se vende en la unidad del producto (X CAJA, X M2 o precio por pieza); cero si no
    /// se puede determinar. Es derivado: no se guarda.
    /// </summary>
    public Money Price => PriceFor(InitialMode) is { } cents ? Money.FromCents(cents) : Money.Zero;

    public bool IsTile => UnitOfMeasure.IsTile(UnitCode);

    /// <summary>Modalidad con la que inicia una partida: la de la unidad del producto.</summary>
    public SaleMode InitialMode => ProductPricing.InitialMode(UnitCode);

    /// <summary>
    /// Modalidades que tienen un precio disponible o derivable. La inicial va primero. Un producto que controla
    /// inventario solo ofrece las que se pueden convertir a su unidad (m² ÷ embalaje = cajas).
    /// </summary>
    public IReadOnlyList<SaleMode> AvailableModes
    {
        get
        {
            if (!IsTile)
            {
                return [SaleMode.Piece];
            }

            return new[] { InitialMode, SaleMode.Box, SaleMode.SquareMeter, SaleMode.Piece }
                .Distinct()
                .Where(m => PriceFor(m) is not null && (m == InitialMode || !TracksInventory || ToProductUnitThousandths(m, 1000) is not null))
                .ToList();
        }
    }

    /// <summary>Unidad de medida en la que se vende en la modalidad dada.</summary>
    public string UnitCodeFor(SaleMode mode) => IsTile ? mode.UnitCode() : UnitCode;

    /// <summary>
    /// Precio unitario, en centavos, para la modalidad: caja = X CAJA o X M2 × Embalaje; metro cuadrado =
    /// X M2 o X CAJA ÷ Embalaje; pieza = solo el precio por pieza. Derivados redondeados al centavo.
    /// Nulo si no hay forma de obtenerlo.
    /// </summary>
    public long? PriceFor(SaleMode mode) =>
        ProductPricing.PriceFor(mode, IsTile, PricePerSqmCents, PricePerBoxCents, PiecePriceCents, PackagingThousandths);

    /// <summary>
    /// Convierte una cantidad vendida en la modalidad dada a la unidad del producto (m² ÷ embalaje = cajas;
    /// piezas ÷ piezas por caja = cajas; cajas × embalaje = m²), en milésimas con la mitad hacia arriba. Nulo si
    /// la conversión no es posible por falta de embalaje o de piezas por caja.
    /// </summary>
    public long? ToProductUnitThousandths(SaleMode mode, long thousandths)
    {
        var from = UnitCodeFor(mode);
        if (from == UnitCode)
        {
            return thousandths;
        }

        var packaging = PackagingThousandths is > 0 ? PackagingThousandths.Value : 0;
        var perBox = PiecesPerBox is > 0 ? PiecesPerBox.Value : 0;
        return (from, UnitCode) switch
        {
            ("MTK", "XBX") when packaging > 0 => ((thousandths * 1000) + (packaging / 2)) / packaging,
            ("XBX", "MTK") when packaging > 0 => ((thousandths * packaging) + 500) / 1000,
            ("H87", "XBX") when perBox > 0 => (thousandths + (perBox / 2)) / perBox,
            _ => null,
        };
    }

    /// <summary>Clave de la unidad de medida (<see cref="UnitOfMeasure"/>).</summary>
    public string UnitCode { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Indica si el producto controla inventario (FR-001); por omisión no.</summary>
    public bool TracksInventory { get; private set; }

    /// <summary>Existencia mínima para alertas (FR-004). Nulo si no controla inventario o no la define.</summary>
    public long? MinimumStockThousandths { get; private set; }

    public Quantity? MinimumStock => MinimumStockThousandths is { } m ? Quantity.FromThousandths(m) : null;

    /// <summary>
    /// Punto de reorden: al llegar a él la alerta es urgente (022, FR-001). Nulo si no controla
    /// inventario o no lo define; si hay mínimo, es menor que él.
    /// </summary>
    public long? ReorderPointThousandths { get; private set; }

    public Quantity? ReorderPoint => ReorderPointThousandths is { } r ? Quantity.FromThousandths(r) : null;

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
        ProductDetails details,
        string sku,
        string? barcode,
        string unitCode,
        bool tracksInventory = false,
        Quantity? minimumStock = null,
        Guid? categoryId = null,
        Quantity? reorderPoint = null)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            IsActive = true,
            Version = 1,
            CategoryId = categoryId,
        };
        product.Apply(details, sku, barcode, unitCode);
        product.ApplyInventory(tracksInventory, minimumStock, reorderPoint);
        return product;
    }

    /// <summary>
    /// Alta con los datos mínimos: el nombre es el modelo y el precio corresponde a la unidad (caja o
    /// metro cuadrado si es de loseta; pieza en los demás casos).
    /// </summary>
    public static Product Create(
        string name,
        string sku,
        string? barcode,
        Money price,
        string unitCode,
        bool tracksInventory = false,
        Quantity? minimumStock = null,
        Guid? categoryId = null,
        Quantity? reorderPoint = null) =>
        Create(DetailsFromPrice(name, price, unitCode), sku, barcode, unitCode, tracksInventory, minimumStock, categoryId, reorderPoint);

    /// <summary>
    /// Actualiza el producto. Con <paramref name="hasMovements"/> no se puede cambiar la unidad ni
    /// dejar de controlar inventario (FR-006).
    /// </summary>
    public void Update(
        ProductDetails details,
        string sku,
        string? barcode,
        string unitCode,
        bool isActive,
        bool tracksInventory = false,
        Quantity? minimumStock = null,
        bool hasMovements = false,
        Guid? categoryId = null,
        Quantity? reorderPoint = null)
    {
        if (!CanChangeInventorySettings(hasMovements, UnitCode, unitCode, TracksInventory, tracksInventory))
        {
            throw new DomainException(
                "Un producto con movimientos de inventario no puede cambiar de unidad ni dejar de controlar inventario.");
        }

        Apply(details, sku, barcode, unitCode);
        ApplyInventory(tracksInventory, minimumStock, reorderPoint);
        IsActive = isActive;
        CategoryId = categoryId;
    }

    /// <summary>Actualización con los datos mínimos; conserva los demás datos del proveedor.</summary>
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
        Guid? categoryId = null,
        Quantity? reorderPoint = null)
    {
        var basic = DetailsFromPrice(name, price, unitCode);
        var details = basic with
        {
            Measures = Measures,
            Brand = Brand,
            SupplierId = SupplierId,
            PackagingThousandths = PackagingThousandths,
            PiecesPerBox = PiecesPerBox,
        };
        Update(details, sku, barcode, unitCode, isActive, tracksInventory, minimumStock, hasMovements, categoryId, reorderPoint);
    }

    /// <summary>Quita la marca "Para revisión" (026, FR-017a).</summary>
    public void MarkReviewed() => NeedsReview = false;

    public void MarkForReview() => NeedsReview = true;

    /// <summary>
    /// Paso de migración: recalcula las claves a partir de los textos guardados. Un duplicado queda sin
    /// clave de identidad y marcado para revisión.
    /// </summary>
    public void ApplyMigrationKeys(bool keepIdentity, bool needsReview)
    {
        ModelKey = ProductText.NormalizeModel(Model);
        MeasuresKey = ProductText.NormalizeMeasures(Measures);
        BrandSearch = ProductText.NormalizeBrand(Brand);
        IdentityKey = keepIdentity ? ProductText.BuildIdentityKey(SupplierId, Model, Measures) : null;
        NeedsReview = needsReview || !keepIdentity;
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

    /// <summary>Recorta, quita los asteriscos de CODE39 y pasa a mayúsculas; vacío se convierte en nulo (021).</summary>
    public static string? NormalizeBarcode(string? barcode) => BarcodeRules.Normalize(barcode);

    public static bool IsValidName(string normalizedName) =>
        normalizedName.Length is > 0 and <= NameMaxLength;

    public static bool IsValidSku(string normalizedSku) =>
        normalizedSku.Length is > 0 and <= SkuMaxLength && !normalizedSku.Any(char.IsWhiteSpace);

    public static bool IsValidBarcode(string? normalizedBarcode) =>
        BarcodeRules.IsValidForCatalog(normalizedBarcode);

    /// <summary>El precio de venta debe ser mayor que 0 (003, FR-015).</summary>
    public static bool IsValidPrice(Money price) => price.Cents > 0;

    /// <summary>El embalaje, si existe, es mayor que 0 y cabe en las milésimas permitidas (026).</summary>
    public static bool IsValidPackaging(long? thousandths) =>
        thousandths is null or (> 0 and <= Quantity.MaxCaptureThousandths);

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
    /// El punto de reorden, si existe, respeta los decimales de la unidad y, si hay mínimo, es
    /// estrictamente menor que él (022, FR-002). 0 es válido: urgente solo al agotarse.
    /// </summary>
    public static bool IsValidReorderPoint(Quantity? reorder, Quantity? minimum, UnitOfMeasure unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return reorder is null
            || (IsValidMinimumStock(reorder, unit) && (minimum is null || reorder.Value < minimum.Value));
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

    /// <summary>
    /// Indica si un texto de búsqueda tiene la forma de un código de barras numérico completo. Los
    /// códigos alfanuméricos se encuentran con la búsqueda por contenido.
    /// </summary>
    public static bool LooksLikeFullBarcode(string text) => FullNumericBarcodePattern().IsMatch(text);

    private static ProductDetails DetailsFromPrice(string name, Money price, string unitCode) => new(
        name,
        PricePerSqmCents: unitCode == "MTK" ? price.Cents : null,
        PricePerBoxCents: unitCode == "XBX" ? price.Cents : null,
        PiecePriceCents: UnitOfMeasure.IsTile(unitCode) ? null : price.Cents);

    private void Apply(ProductDetails details, string sku, string? barcode, string unitCode)
    {
        ArgumentNullException.ThrowIfNull(details);
        var model = NormalizeName(details.Model);
        var measures = NormalizeOptional(details.Measures);
        var brand = NormalizeOptional(details.Brand);
        var normalizedSku = NormalizeSku(sku);
        var normalizedBarcode = NormalizeBarcode(barcode);

        if (!IsValidName(model))
        {
            throw new DomainException($"El modelo es obligatorio y admite hasta {NameMaxLength} caracteres.");
        }

        if (measures is { Length: > MeasuresMaxLength })
        {
            throw new DomainException($"Las medidas admiten hasta {MeasuresMaxLength} caracteres.");
        }

        if (brand is { Length: > BrandMaxLength })
        {
            throw new DomainException($"La marca admite hasta {BrandMaxLength} caracteres.");
        }

        if (!IsValidSku(normalizedSku))
        {
            throw new DomainException($"El SKU es obligatorio, sin espacios y hasta {SkuMaxLength} caracteres.");
        }

        if (!IsValidBarcode(normalizedBarcode))
        {
            throw new DomainException(
                $"El código de barras admite hasta {BarcodeMaxLength} letras, dígitos, espacios interiores y los símbolos - . $ / + %.");
        }

        if (!UnitOfMeasure.IsValidCode(unitCode))
        {
            throw new DomainException("La unidad de medida no es válida.");
        }

        var isTile = UnitOfMeasure.IsTile(unitCode);
        ValidateOptionalPositive(details.PricePerSqmCents, Money.MaxCents, "El precio por metro cuadrado");
        ValidateOptionalPositive(details.PricePerBoxCents, Money.MaxCents, "El precio por caja");
        ValidateOptionalPositive(details.PiecePriceCents, Money.MaxCents, "El precio por pieza");
        if (isTile)
        {
            ValidateOptionalPositive(details.PackagingThousandths, Quantity.MaxCaptureThousandths, "El embalaje");
            ValidateOptionalPositive(details.PiecesPerBox, int.MaxValue, "Las piezas por caja");
            if (details.PricePerSqmCents is null && details.PricePerBoxCents is null)
            {
                throw new DomainException("Una loseta necesita al menos el precio por metro cuadrado o el precio por caja.");
            }
        }
        else if (details.PiecePriceCents is null)
        {
            throw new DomainException("El precio debe ser mayor que 0.");
        }

        Model = model;
        ModelKey = ProductText.NormalizeModel(model);
        Measures = measures;
        MeasuresKey = ProductText.NormalizeMeasures(measures);
        Brand = brand;
        BrandSearch = ProductText.NormalizeBrand(brand);
        SupplierId = details.SupplierId;
        PackagingThousandths = isTile ? details.PackagingThousandths : null;
        PiecesPerBox = isTile ? details.PiecesPerBox : null;
        PricePerSqmCents = isTile ? details.PricePerSqmCents : null;
        PricePerBoxCents = isTile ? details.PricePerBoxCents : null;
        PiecePriceCents = details.PiecePriceCents;
        IdentityKey = ProductText.BuildIdentityKey(details.SupplierId, model, measures);
        NeedsReview = false;

        Name = ProductText.BuildName(model, measures);
        NameSearch = TextNormalizer.ForSearch(Name);
        Sku = normalizedSku;
        Barcode = normalizedBarcode;
        UnitCode = unitCode;
    }

    private static string? NormalizeOptional(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static void ValidateOptionalPositive(long? value, long max, string what)
    {
        if (value is { } v && (v <= 0 || v > max))
        {
            throw new DomainException($"{what} debe ser mayor que 0.");
        }
    }

    private void ApplyInventory(bool tracksInventory, Quantity? minimumStock, Quantity? reorderPoint)
    {
        var unit = UnitOfMeasure.Find(UnitCode)!;
        if (tracksInventory && !IsValidMinimumStock(minimumStock, unit))
        {
            throw new DomainException("La existencia mínima no es válida para la unidad del producto.");
        }

        if (tracksInventory && !IsValidReorderPoint(reorderPoint, minimumStock, unit))
        {
            throw new DomainException(
                "El punto de reorden no es válido para la unidad del producto o no es menor que la existencia mínima.");
        }

        TracksInventory = tracksInventory;
        MinimumStockThousandths = tracksInventory ? minimumStock?.Thousandths : null;
        ReorderPointThousandths = tracksInventory ? reorderPoint?.Thousandths : null;
    }

    [GeneratedRegex(@"^[0-9]{8,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullNumericBarcodePattern();
}
