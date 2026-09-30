using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.GetProduct;
using Pos.Application.Products.UpdateProduct;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Common;

namespace Pos.Desktop.Products;

/// <summary>Alta y edición de un producto. Solo coordina la interfaz; las reglas viven en Application.</summary>
public sealed partial class ProductEditorViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;

    private Guid? _productId;
    private int _expectedVersion;

    public ProductEditorViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
    }

    /// <summary>El producto se guardó; incluye sus datos actuales.</summary>
    public event EventHandler<ProductDto>? Saved;

    /// <summary>El editor se cerró sin guardar.</summary>
    public event EventHandler? Closed;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Sku { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Barcode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial string? NameError { get; set; }

    [ObservableProperty]
    public partial string? SkuError { get; set; }

    [ObservableProperty]
    public partial string? BarcodeError { get; set; }

    [ObservableProperty]
    public partial string? PriceError { get; set; }

    /// <summary>Campo que debe recibir el foco (el primero con error).</summary>
    [ObservableProperty]
    public partial string? FocusField { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public string Title => IsEditMode ? Strings.Editor_EditTitle : Strings.Editor_NewTitle;

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
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
            return false;
        }

        Fill(result.Value);
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ClearErrors();
        var (completed, result) = await _runner.RunAsync(
            "GuardarProducto",
            () => _productId is { } id
                ? _useCases.RunAsync<UpdateProductHandler, Result<ProductDto>>(h => h.HandleAsync(
                    new UpdateProductCommand(id, _expectedVersion, Name, Sku, Barcode, PriceText, IsActive),
                    CancellationToken.None))
                : _useCases.RunAsync<CreateProductHandler, Result<ProductDto>>(h => h.HandleAsync(
                    new CreateProductCommand(Name, Sku, Barcode, PriceText),
                    CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = _productId, ["Sku"] = Sku });

        if (!completed || result is null)
        {
            return;
        }

        if (result.IsSuccess)
        {
            Saved?.Invoke(this, result.Value);
            return;
        }

        await ShowErrorAsync(result.Error);
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);

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
                if (await _dialogs.ConfirmAsync(Strings.Editor_ConflictTitle, Strings.Editor_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;

            case NotFound:
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
                Closed?.Invoke(this, EventArgs.Empty);
                break;

            default:
                await _dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
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
        IsActive = product.IsActive;
        ClearErrors();
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
        }
    }

    private void ClearErrors()
    {
        NameError = SkuError = BarcodeError = PriceError = null;
        FocusField = null;
    }
}
