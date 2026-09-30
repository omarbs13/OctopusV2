using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.GetProduct;
using Pos.Application.Products.ListUnitsOfMeasure;
using Pos.Application.Products.PrepareProductImage;
using Pos.Application.Products.UpdateProduct;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
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
    private long? _onHandThousandths;

    /// <summary>Cambio de imagen pendiente; se aplica solo al guardar (003, FR-025).</summary>
    private ProductImageChange _imageChange = ProductImageChange.KeepCurrent;

    public ProductEditorViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        ResetOriginalState();
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Sku { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Barcode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    /// <summary>Clave de la unidad de medida; "Pieza" por defecto en alta (003, FR-017).</summary>
    [ObservableProperty]
    public partial string UnitCode { get; set; } = UnitOfMeasure.Default.Code;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>Controla inventario (FR-001); por omisión no.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnHandText))]
    public partial bool TracksInventory { get; set; }

    /// <summary>Existencia mínima capturada; vacío significa sin mínimo.</summary>
    [ObservableProperty]
    public partial string MinimumStockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? MinimumStockError { get; set; }

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
    public partial string? NameError { get; set; }

    [ObservableProperty]
    public partial string? SkuError { get; set; }

    [ObservableProperty]
    public partial string? BarcodeError { get; set; }

    [ObservableProperty]
    public partial string? PriceError { get; set; }

    [ObservableProperty]
    public partial string? UnitCodeError { get; set; }

    partial void OnUnitCodeChanged(string value) => OnPropertyChanged(nameof(OnHandText));

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

    public bool IsNameRequired { get; } = true;

    public bool IsSkuRequired { get; } = true;

    public bool IsBarcodeRequired { get; }

    public bool IsPriceRequired { get; } = true;

    public bool IsUnitRequired { get; } = true;

    /// <summary>Catálogo fijo; no depende de la base, así que se obtiene sin ámbito de caso de uso.</summary>
    public IReadOnlyList<UnitOfMeasureDto> Units { get; } = new ListUnitsOfMeasureHandler().Handle();

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
                    new UpdateProductCommand(id, _expectedVersion, Name, Sku, Barcode, PriceText, UnitCode, IsActive, _imageChange, TracksInventory, MinimumStockText),
                    CancellationToken.None))
                : _useCases.RunAsync<CreateProductHandler, Result<ProductDto>>(h => h.HandleAsync(
                    new CreateProductCommand(Name, Sku, Barcode, PriceText, UnitCode, _imageChange, TracksInventory, MinimumStockText),
                    CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = _productId, ["Sku"] = Sku });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.IsSuccess)
        {
            OnSaved(result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    /// <summary>Valores como se guardarían: revertir un cambio o escribir el SKU en minúsculas no cuenta como cambio.</summary>
    protected override object CaptureState() => new ProductFormState(
        Product.NormalizeName(Name),
        Product.NormalizeSku(Sku),
        Product.NormalizeBarcode(Barcode),
        Money.TryParse(PriceText, out var price) ? price.Cents.ToString(System.Globalization.CultureInfo.InvariantCulture) : PriceText.Trim(),
        UnitCode,
        IsActive,
        _imageChange,
        TracksInventory,
        TracksInventory ? MinimumStockText.Trim() : string.Empty);

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

            case Duplicate duplicate:
                SetError(
                    duplicate.Field,
                    duplicate.Field == ProductFields.Barcode ? Strings.Editor_DuplicateBarcode : Strings.Editor_DuplicateSku);
                FocusField = duplicate.Field;
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
        Name = product.Name;
        Sku = product.Sku;
        Barcode = product.Barcode ?? string.Empty;
        PriceText = Money.FromCents(product.PriceCents).ToEditableString();
        UnitCode = product.UnitCode;
        IsActive = product.IsActive;
        TracksInventory = product.TracksInventory;
        MinimumStockText = product.MinimumStockThousandths is { } minimum
            ? Quantity.FromThousandths(minimum).ToEditableString(product.DecimalPlaces)
            : string.Empty;
        _onHandThousandths = product.OnHandThousandths;
        HasMovements = product.HasMovements;
        OnPropertyChanged(nameof(OnHandText));
        PreviewImage = product.Image;
        ImageError = null;
        _imageChange = ProductImageChange.KeepCurrent;
        ClearErrors();
        ResetOriginalState();
    }

    private void SetError(string field, string message)
    {
        switch (field)
        {
            case ProductFields.Name:
                NameError = message;
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
            case ProductFields.TracksInventory:
                TracksInventoryError = message;
                break;
        }
    }

    private void ClearErrors()
    {
        NameError = SkuError = BarcodeError = PriceError = UnitCodeError = MinimumStockError = TracksInventoryError = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de producto, para detectar cambios.</summary>
/// <remarks>El cambio de imagen se compara por instancia: elegir otra imagen siempre cuenta como cambio.</remarks>
internal sealed record ProductFormState(
    string Name,
    string Sku,
    string? Barcode,
    string Price,
    string UnitCode,
    bool IsActive,
    ProductImageChange Image,
    bool TracksInventory,
    string MinimumStock);
