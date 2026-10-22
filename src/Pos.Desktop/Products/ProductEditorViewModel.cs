using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Licensing;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.GetProduct;
using Pos.Application.Products.ListSupplierOptions;
using Pos.Application.Products.ListUnitsOfMeasure;
using Pos.Application.Products.PrepareProductImage;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Reports.SetProductCritical;
using Pos.Application.Suppliers;
using Pos.Desktop.Categories;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using System.Collections.ObjectModel;
using System.Globalization;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Products;

/// <summary>
/// Alta y edición de un producto (formulario corto). Solo coordina la interfaz; las reglas viven
/// en Application.
/// </summary>
public sealed partial class ProductEditorViewModel : FormViewModel<ProductDto>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private static readonly IReadOnlyList<FileTypeFilter> ImageFilters =
        [new(Strings.Editor_ImageFilter, ["*.jpg", "*.jpeg", "*.png", "*.webp"])];

    private Guid? _productId;
    private int _expectedVersion;
    private bool _loadedIsCritical;
    private long? _onHandThousandths;
    private Guid? _loadedCategoryId;
    private string? _loadedCategoryName;

    /// <summary>Cambio de imagen pendiente; se aplica solo al guardar (003, FR-025).</summary>
    private ProductImageChange _imageChange = ProductImageChange.KeepCurrent;

    public ProductEditorViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, ILicenseState? license = null)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        Category = new CategoryPickerViewModel(useCases, runner, CategoryPickerMode.Assignment, license);
        ResetOriginalState();
    }

    /// <summary>Campo "Categoría" (016, FR-010): "Sin categoría" y las activas; la actual inactiva solo si ya la tenía.</summary>
    public CategoryPickerViewModel Category { get; }

    [ObservableProperty]
    public partial string? CategoryError { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Measures { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Brand { get; set; } = string.Empty;

    /// <summary>Proveedor elegido (026); la primera opción es "Sin proveedor".</summary>
    [ObservableProperty]
    public partial ProductSupplierChoice? SelectedSupplier { get; set; }

    public ObservableCollection<ProductSupplierChoice> SupplierOptions { get; } = [];

    /// <summary>Metros cuadrados por caja; vacío o "NA" = no aplica.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoxPriceSuggestion), nameof(SquareMeterPriceSuggestion), nameof(PriceWarning))]
    public partial string PackagingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PiecesPerBoxText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoxPriceSuggestion), nameof(PriceWarning))]
    public partial string PricePerSqmText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SquareMeterPriceSuggestion), nameof(PriceWarning))]
    public partial string PricePerBoxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Sku { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Barcode { get; set; } = string.Empty;

    /// <summary>Precio por pieza (el precio de los productos que no son de loseta).</summary>
    [ObservableProperty]
    public partial string PiecePriceText { get; set; } = string.Empty;

    /// <summary>Clave de la unidad de medida; "Pieza" por defecto en alta (003, FR-017).</summary>
    [ObservableProperty]
    public partial string UnitCode { get; set; } = UnitOfMeasure.Default.Code;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>Controla inventario (FR-001); por omisión no.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnHandText))]
    public partial bool TracksInventory { get; set; }

    /// <summary>Producto crítico (009): sus existencias bajas aparecen en las alertas de Inicio.</summary>
    [ObservableProperty]
    public partial bool IsCritical { get; set; }

    /// <summary>Existencia mínima capturada; vacío significa sin mínimo.</summary>
    [ObservableProperty]
    public partial string MinimumStockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? MinimumStockError { get; set; }

    /// <summary>Punto de reorden capturado (022); vacío significa sin punto de reorden.</summary>
    [ObservableProperty]
    public partial string ReorderPointText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReorderPointError { get; set; }

    [ObservableProperty]
    public partial string? TracksInventoryError { get; set; }

    /// <summary>Con movimientos no se puede cambiar la unidad ni dejar de controlar inventario (FR-006).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeInventorySettings))]
    public partial bool HasMovements { get; private set; }

    public bool CanChangeInventorySettings => !HasMovements;

    /// <summary>Existencia actual con su unidad, de solo lectura (FR-005); "—" si no controla inventario.</summary>
    public string OnHandText
    {
        get
        {
            if (!TracksInventory)
            {
                return QuantityConverter.NoValue;
            }

            var unit = UnitOfMeasure.Find(UnitCode) ?? UnitOfMeasure.Default;
            return $"{QuantityConverter.Format(_onHandThousandths ?? 0, unit.DecimalPlaces)} {unit.Name}";
        }
    }

    [ObservableProperty]
    public partial string? ModelError { get; set; }

    [ObservableProperty]
    public partial string? MeasuresError { get; set; }

    [ObservableProperty]
    public partial string? BrandError { get; set; }

    [ObservableProperty]
    public partial string? SupplierError { get; set; }

    [ObservableProperty]
    public partial string? PackagingError { get; set; }

    [ObservableProperty]
    public partial string? PiecesPerBoxError { get; set; }

    [ObservableProperty]
    public partial string? PricePerSqmError { get; set; }

    [ObservableProperty]
    public partial string? PricePerBoxError { get; set; }

    [ObservableProperty]
    public partial string? SkuError { get; set; }

    [ObservableProperty]
    public partial string? BarcodeError { get; set; }

    [ObservableProperty]
    public partial string? PriceError { get; set; }

    [ObservableProperty]
    public partial string? UnitCodeError { get; set; }

    partial void OnUnitCodeChanged(string value)
    {
        OnPropertyChanged(nameof(OnHandText));
        OnPropertyChanged(nameof(IsTile));
        OnPropertyChanged(nameof(IsNotTile));
    }

    /// <summary>Loseta: unidad caja o metro cuadrado; solo entonces se piden embalaje, piezas y precios por m² y caja.</summary>
    public bool IsTile => UnitOfMeasure.IsTile(UnitCode);

    public bool IsNotTile => !IsTile;

    /// <summary>X CAJA sugerido (X M2 × Embalaje) mientras el campo está vacío; nulo si no aplica.</summary>
    public string? BoxPriceSuggestion =>
        string.IsNullOrWhiteSpace(PricePerBoxText)
        && ProductPricing.SuggestBoxPrice(ParseCents(PricePerSqmText), ParsePackaging(PackagingText)) is { } cents
            ? Money.FromCents(cents).ToEditableString()
            : null;

    /// <summary>X M2 sugerido (X CAJA ÷ Embalaje) mientras el campo está vacío; nulo si no aplica.</summary>
    public string? SquareMeterPriceSuggestion =>
        string.IsNullOrWhiteSpace(PricePerSqmText)
        && ProductPricing.SuggestSquareMeterPrice(ParseCents(PricePerBoxText), ParsePackaging(PackagingText)) is { } cents
            ? Money.FromCents(cents).ToEditableString()
            : null;

    /// <summary>Advertencia (no bloquea) si X CAJA difiere más del 2 % de X M2 × Embalaje.</summary>
    public string? PriceWarning =>
        ParseCents(PricePerBoxText) is { } box && ParseCents(PricePerSqmText) is { } sqm && ParsePackaging(PackagingText) is { } packaging
        && ProductPricing.ExceedsTolerance(box, sqm, packaging)
            ? ProductMessages.PriceDifference
            : null;

    [RelayCommand]
    private void UseBoxPriceSuggestion()
    {
        if (BoxPriceSuggestion is { } value)
        {
            PricePerBoxText = value;
        }
    }

    [RelayCommand]
    private void UseSquareMeterPriceSuggestion()
    {
        if (SquareMeterPriceSuggestion is { } value)
        {
            PricePerSqmText = value;
        }
    }

    private static long? ParseCents(string text) => Money.TryParse(text, out var money) && money.Cents > 0 ? money.Cents : null;

    private static long? ParsePackaging(string text)
    {
        var result = Quantity.Parse(text, decimalPlaces: 3);
        return result.Value?.Thousandths;
    }

    /// <summary>Imagen que se muestra en la vista previa: la actual o la recién seleccionada.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyCanExecuteChangedFor(nameof(RemoveImageCommand))]
    public partial byte[]? PreviewImage { get; private set; }

    [ObservableProperty]
    public partial string? ImageError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveImageCommand))]
    public partial bool IsProcessingImage { get; private set; }

    public bool HasImage => PreviewImage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public override string Title => IsEditMode ? Strings.Editor_EditTitle : Strings.Editor_NewTitle;

    public bool IsModelRequired { get; } = true;

    public bool IsSkuRequired { get; }

    public bool IsBarcodeRequired { get; }

    public bool IsPriceRequired { get; } = true;

    public bool IsUnitRequired { get; } = true;

    /// <summary>Catálogo fijo; no depende de la base, así que se obtiene sin ámbito de caso de uso.</summary>
    public IReadOnlyList<UnitOfMeasureDto> Units { get; } = new ListUnitsOfMeasureHandler().Handle();

    /// <summary>Prepara el alta: carga las categorías activas para el selector.</summary>
    public async Task PrepareNewAsync()
    {
        await Category.LoadAsync();
        await LoadSuppliersAsync(null);
        ResetOriginalState();
    }

    /// <summary>Carga los proveedores asignables; el actual del producto se conserva aunque esté inactivo.</summary>
    private async Task LoadSuppliersAsync(Guid? currentSupplierId)
    {
        var (completed, result) = await _runner.RunAsync(
            "ListarProveedoresProducto",
            () => _useCases.RunAsync<ListProductSupplierOptionsHandler, Result<IReadOnlyList<SupplierOption>>>(
                h => h.HandleAsync(currentSupplierId, CancellationToken.None)));

        SupplierOptions.Clear();
        var none = new ProductSupplierChoice(null, Strings.Editor_NoSupplier);
        SupplierOptions.Add(none);
        if (completed && result is { IsSuccess: true })
        {
            foreach (var supplier in result.Value)
            {
                SupplierOptions.Add(new ProductSupplierChoice(supplier.Id, supplier.Name));
            }
        }

        SelectedSupplier = SupplierOptions.FirstOrDefault(o => o.Id == currentSupplierId) ?? none;
    }

    /// <summary>Carga un producto para editarlo. Devuelve falso si ya no existe (y lo informa).</summary>
    public async Task<bool> LoadAsync(Guid productId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarProducto",
            () => _useCases.RunAsync<GetProductHandler, Result<ProductDto>>(
                h => h.HandleAsync(new GetProductQuery(productId), CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = productId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
            return false;
        }

        Fill(result.Value);
        await Category.LoadAsync(_loadedCategoryId, _loadedCategoryName);
        await LoadSuppliersAsync(result.Value.SupplierId);
        ResetOriginalState();
        return true;
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        if (IsProcessingImage)
        {
            return false;
        }

        ClearErrors();
        var (completed, result) = await _runner.RunAsync(
            "GuardarProducto",
            () => _productId is { } id
                ? _useCases.RunAsync<UpdateProductHandler, Result<ProductDto>>(h => h.HandleAsync(
                    new UpdateProductCommand(
                        id, _expectedVersion, Model, Sku, Barcode, UnitCode, IsActive, PiecePriceText, PricePerSqmText, PricePerBoxText,
                        PackagingText, PiecesPerBoxText, Measures, Brand, SelectedSupplier?.Id, _imageChange, TracksInventory,
                        MinimumStockText, Category.SelectedCategoryId, ReorderPointText),
                    CancellationToken.None))
                : _useCases.RunAsync<CreateProductHandler, Result<ProductDto>>(h => h.HandleAsync(
                    new CreateProductCommand(
                        Model, Sku, Barcode, UnitCode, PiecePriceText, PricePerSqmText, PricePerBoxText, PackagingText, PiecesPerBoxText,
                        Measures, Brand, SelectedSupplier?.Id, _imageChange, TracksInventory, MinimumStockText,
                        Category.SelectedCategoryId, ReorderPointText),
                    CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = _productId, ["Sku"] = Sku });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.IsSuccess)
        {
            var saved = result.Value;
            if (IsCritical != _loadedIsCritical && !await SaveCriticalAsync(saved.Id))
            {
                return false;
            }

            _loadedIsCritical = IsCritical;
            OnSaved(saved with { IsCritical = IsCritical });
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    /// <summary>La marca de crítico se guarda aparte, con su caso de uso, solo cuando cambió (009).</summary>
    private async Task<bool> SaveCriticalAsync(Guid productId)
    {
        var (completed, result) = await _runner.RunAsync(
            "MarcarProductoCritico",
            () => _useCases.RunAsync<SetProductCriticalHandler, Result>(
                h => h.HandleAsync(new SetProductCriticalCommand(productId, IsCritical), CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = productId });
        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await ShowErrorAsync(result.Error);
            return false;
        }

        return true;
    }

    /// <summary>Valores como se guardarían: revertir un cambio o escribir el SKU en minúsculas no cuenta como cambio.</summary>
    protected override object CaptureState() => new ProductFormState(
        Product.NormalizeName(Model),
        Product.NormalizeSku(Sku),
        Product.NormalizeBarcode(Barcode),
        MoneyState(PiecePriceText),
        MoneyState(PricePerSqmText),
        MoneyState(PricePerBoxText),
        PackagingText.Trim().ToUpperInvariant(),
        PiecesPerBoxText.Trim(),
        Measures.Trim(),
        Brand.Trim(),
        SelectedSupplier?.Id,
        UnitCode,
        IsActive,
        _imageChange,
        TracksInventory,
        TracksInventory ? MinimumStockText.Trim() : string.Empty,
        TracksInventory ? ReorderPointText.Trim() : string.Empty,
        TracksInventory && IsCritical,
        Category.SelectedCategoryId);

    private static string MoneyState(string text) =>
        Money.TryParse(text, out var money) ? money.Cents.ToString(CultureInfo.InvariantCulture) : text.Trim();

    [RelayCommand(CanExecute = nameof(CanChangeImage))]
    private async Task SelectImageAsync()
    {
        var file = await Dialogs.PickOpenFileAsync(Strings.Editor_SelectImage, ImageFilters);
        if (file is null)
        {
            return;
        }

        IsProcessingImage = true;
        try
        {
            var (completed, result) = await _runner.RunAsync(
                "PrepararImagenProducto",
                async () =>
                {
                    await using var content = await file.OpenAsync();
                    return await _useCases.RunAsync<PrepareProductImageHandler, Result<PreparedProductImage>>(
                        h => h.HandleAsync(new PrepareProductImageCommand(content, file.Length), CancellationToken.None));
                },
                new Dictionary<string, object?> { ["ProductId"] = _productId, ["FileName"] = file.Name, ["Length"] = file.Length });

            if (!completed || result is null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                _imageChange = new ProductImageChange.Replace(result.Value);
                PreviewImage = result.Value.Content;
                ImageError = null;
            }
            else if (result.Error is InvalidImage invalid)
            {
                // La imagen anterior no cambia (003, FR-023).
                ImageError = PrepareProductImageHandler.MessageFor(invalid.Reason);
            }
        }
        finally
        {
            IsProcessingImage = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    private void RemoveImage()
    {
        _imageChange = _productId is null ? ProductImageChange.KeepCurrent : new ProductImageChange.Remove();
        PreviewImage = null;
        ImageError = null;
    }

    private bool CanChangeImage() => !IsProcessingImage;

    private bool CanRemoveImage() => !IsProcessingImage && HasImage;

    private async Task ShowErrorAsync(Error error)
    {
        switch (error)
        {
            case ValidationFailed validation:
                foreach (var field in validation.Errors)
                {
                    SetError(field.Field, field.Message);
                }

                FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
                break;

            case Duplicate { Field: ProductFields.Identity } identity:
                ModelError = string.Format(
                    CultureInfo.CurrentCulture, Strings.Editor_DuplicateIdentity, identity.ExistingName, identity.ExistingSku);
                FocusField = ProductFields.Model;
                break;

            case Duplicate duplicate:
                SetError(
                    duplicate.Field,
                    duplicate.Field == ProductFields.Barcode ? Strings.Editor_DuplicateBarcode : Strings.Editor_DuplicateSku);
                FocusField = duplicate.Field;
                break;

            case CategoryNotAssignable:
                // La categoría elegida se desactivó o eliminó mientras se editaba (spec, casos límite).
                CategoryError = CategoryMessages.NotAssignable;
                FocusField = CategoryFields.Category;
                await Category.LoadAsync(_loadedCategoryId, _loadedCategoryName);
                break;

            case Conflict when _productId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Editor_ConflictTitle, Strings.Editor_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;

            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
                RaiseClosed();
                break;

            default:
                await Dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                break;
        }
    }

    private void Fill(ProductDto product)
    {
        _productId = product.Id;
        _expectedVersion = product.Version;
        IsEditMode = true;
        Model = product.Model;
        Measures = product.Measures ?? string.Empty;
        Brand = product.Brand ?? string.Empty;
        PackagingText = product.PackagingThousandths is { } packaging
            ? Quantity.FromThousandths(packaging).ToEditableString(3)
            : string.Empty;
        PiecesPerBoxText = product.PiecesPerBox?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        PricePerSqmText = CentsText(product.PricePerSqmCents);
        PricePerBoxText = CentsText(product.PricePerBoxCents);
        PiecePriceText = CentsText(product.PiecePriceCents);
        Sku = product.Sku;
        Barcode = product.Barcode ?? string.Empty;
        UnitCode = product.UnitCode;
        IsActive = product.IsActive;
        TracksInventory = product.TracksInventory;
        IsCritical = product.IsCritical;
        _loadedIsCritical = product.IsCritical;
        MinimumStockText = product.MinimumStockThousandths is { } minimum
            ? Quantity.FromThousandths(minimum).ToEditableString(product.DecimalPlaces)
            : string.Empty;
        ReorderPointText = product.ReorderPointThousandths is { } reorder
            ? Quantity.FromThousandths(reorder).ToEditableString(product.DecimalPlaces)
            : string.Empty;
        _onHandThousandths = product.OnHandThousandths;
        _loadedCategoryId = product.CategoryId;
        _loadedCategoryName = product.CategoryName;
        HasMovements = product.HasMovements;
        OnPropertyChanged(nameof(OnHandText));
        PreviewImage = product.Image;
        ImageError = null;
        _imageChange = ProductImageChange.KeepCurrent;
        ClearErrors();
        ResetOriginalState();
    }

    private static string CentsText(long? cents) => cents is { } value ? Money.FromCents(value).ToEditableString() : string.Empty;

    private void SetError(string field, string message)
    {
        switch (field)
        {
            case ProductFields.Model:
                ModelError = message;
                break;
            case ProductFields.Measures:
                MeasuresError = message;
                break;
            case ProductFields.Brand:
                BrandError = message;
                break;
            case ProductFields.Supplier:
                SupplierError = message;
                break;
            case ProductFields.Packaging:
                PackagingError = message;
                break;
            case ProductFields.PiecesPerBox:
                PiecesPerBoxError = message;
                break;
            case ProductFields.PricePerSqm:
                PricePerSqmError = message;
                break;
            case ProductFields.PricePerBox:
                PricePerBoxError = message;
                break;
            case ProductFields.Sku:
                SkuError = message;
                break;
            case ProductFields.Barcode:
                BarcodeError = message;
                break;
            case ProductFields.Price:
                PriceError = message;
                break;
            case ProductFields.UnitCode:
                UnitCodeError = message;
                break;
            case ProductFields.MinimumStock:
                MinimumStockError = message;
                break;
            case ProductFields.ReorderPoint:
                ReorderPointError = message;
                break;
            case ProductFields.TracksInventory:
                TracksInventoryError = message;
                break;
        }
    }

    private void ClearErrors()
    {
        ModelError = MeasuresError = BrandError = SupplierError = PackagingError = PiecesPerBoxError = PricePerSqmError = PricePerBoxError = null;
        SkuError = BarcodeError = PriceError = UnitCodeError = MinimumStockError = ReorderPointError = TracksInventoryError = CategoryError = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de producto, para detectar cambios.</summary>
/// <remarks>El cambio de imagen se compara por instancia: elegir otra imagen siempre cuenta como cambio.</remarks>
internal sealed record ProductFormState(
    string Model,
    string Sku,
    string? Barcode,
    string PiecePrice,
    string PricePerSqm,
    string PricePerBox,
    string Packaging,
    string PiecesPerBox,
    string Measures,
    string Brand,
    Guid? SupplierId,
    string UnitCode,
    bool IsActive,
    ProductImageChange Image,
    bool TracksInventory,
    string MinimumStock,
    string ReorderPoint,
    bool IsCritical,
    Guid? CategoryId);

/// <summary>Opción del selector de proveedor del producto; <c>Id</c> nulo = "Sin proveedor".</summary>
public sealed record ProductSupplierChoice(Guid? Id, string Label)
{
    public override string ToString() => Label;
}
