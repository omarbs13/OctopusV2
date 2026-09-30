using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Inventory.SearchStock;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Common;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Inventory;

/// <summary>
/// Formulario "Registrar movimiento" (contracts/ui.md). Solo coordina la interfaz; la regla
/// autoritativa es <see cref="RegisterMovementHandler"/>. La vista previa de la existencia resultante
/// usa <see cref="Quantity"/> únicamente para mostrarla (Principio III).
/// </summary>
public sealed partial class MovementEditorViewModel : FormViewModel<MovementDto>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    public MovementEditorViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        RefreshTypes();
        ResetOriginalState();
    }

    public override string Title => Strings.MovementEditor_Title;

    /// <summary>Selector de producto; nulo cuando el producto viene fijo (desde Existencias).</summary>
    public ProductPickerViewModel? Picker { get; private set; }

    public bool ShowPicker => Picker is not null;

    public bool IsProductFixed => Picker is null;

    public ObservableCollection<MovementTypeOption> TypeOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProductName), nameof(OnHandText), nameof(QuantityHint))]
    public partial StockItemDto? Product { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReasonRequired))]
    public partial MovementTypeOption? SelectedType { get; set; }

    [ObservableProperty]
    public partial string QuantityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Reference { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ProductError { get; set; }

    [ObservableProperty]
    public partial string? TypeError { get; set; }

    [ObservableProperty]
    public partial string? QuantityError { get; set; }

    [ObservableProperty]
    public partial string? ReasonError { get; set; }

    [ObservableProperty]
    public partial string? ReferenceError { get; set; }

    /// <summary>Existencia después del movimiento, calculada solo para mostrarla.</summary>
    [ObservableProperty]
    public partial string ResultingText { get; private set; } = QuantityConverter.NoValue;

    /// <summary>La vista previa quedaría por debajo de cero (se muestra en rojo).</summary>
    [ObservableProperty]
    public partial bool ResultingNegative { get; private set; }

    public string ProductName => Product?.Name ?? string.Empty;

    public string OnHandText => Product is { } p
        ? $"{QuantityConverter.Format(p.OnHandThousandths, p.DecimalPlaces)} {p.UnitName}"
        : QuantityConverter.NoValue;

    public string QuantityHint => Product is { DecimalPlaces: > 0 }
        ? Strings.MovementEditor_DecimalHint
        : Strings.MovementEditor_WholeHint;

    public bool IsReasonRequired => SelectedType?.Type?.RequiresReason() == true;

    /// <summary>El producto ya viene elegido (desde Existencias).</summary>
    public void InitializeFixed(StockItemDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        Picker = null;
        SetProduct(product);
        ResetOriginalState();
    }

    /// <summary>El operador elige el producto entre los activos que controlan inventario (desde Movimientos).</summary>
    public async Task InitializeWithPickerAsync()
    {
        var picker = new ProductPickerViewModel(_useCases, _runner, includeInactive: false);
        picker.SelectionChanged += (_, _) => SetProduct(picker.Selected?.Stock);
        Picker = picker;
        OnPropertyChanged(nameof(ShowPicker));
        OnPropertyChanged(nameof(IsProductFixed));
        await picker.LoadAsync();
        ResetOriginalState();
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();
        if (Product is not { } product || SelectedType?.Type is not { } type)
        {
            ProductError = Strings.MovementEditor_ProductRequired;
            FocusField = InventoryFields.ProductId;
            return false;
        }

        var (completed, result) = await _runner.RunAsync(
            "RegistrarMovimiento",
            () => _useCases.RunAsync<RegisterMovementHandler, Result<MovementDto>>(h => h.HandleAsync(
                new RegisterMovementCommand(product.ProductId, type, QuantityText, Reason, Reference),
                CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = product.ProductId, ["Type"] = type.ToString() });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.IsSuccess)
        {
            OnSaved(result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error, product);
        return false;
    }

    protected override object CaptureState() => new MovementFormState(
        Product?.ProductId,
        SelectedType?.Type,
        QuantityText.Trim(),
        Reason.Trim(),
        Reference.Trim());

    partial void OnSelectedTypeChanged(MovementTypeOption? value) => UpdatePreview();

    partial void OnQuantityTextChanged(string value) => UpdatePreview();

    private void SetProduct(StockItemDto? product)
    {
        Product = product;
        RefreshTypes();
        UpdatePreview();
    }

    /// <summary>"Inventario inicial" solo aparece si el producto no tiene movimientos, y entonces es el predeterminado.</summary>
    private void RefreshTypes()
    {
        var hasMovements = Product?.HasMovements ?? true;
        var previous = SelectedType?.Type;

        TypeOptions.Clear();
        if (!hasMovements)
        {
            TypeOptions.Add(new MovementTypeOption(MovementType.Initial, MovementTypeLabels.Of(MovementType.Initial)));
        }

        foreach (var type in new[] { MovementType.Receipt, MovementType.AdjustIn, MovementType.AdjustOut })
        {
            TypeOptions.Add(new MovementTypeOption(type, MovementTypeLabels.Of(type)));
        }

        var preferred = previous is { } p && TypeOptions.Any(o => o.Type == p)
            ? p
            : hasMovements ? MovementType.Receipt : MovementType.Initial;
        SelectedType = TypeOptions.First(o => o.Type == preferred);
    }

    private void UpdatePreview()
    {
        ResultingNegative = false;
        ResultingText = QuantityConverter.NoValue;
        if (Product is not { } product || SelectedType?.Type is not { } type)
        {
            return;
        }

        var parsed = Quantity.Parse(QuantityText, product.DecimalPlaces);
        if (parsed.Value is not { } quantity)
        {
            return;
        }

        var resulting = type.IsIncrease()
            ? product.OnHandThousandths + quantity.Thousandths
            : product.OnHandThousandths - quantity.Thousandths;
        ResultingNegative = resulting < 0;
        ResultingText = $"{QuantityConverter.Format(resulting, product.DecimalPlaces)} {product.UnitName}";
    }

    private async Task ShowErrorAsync(Error error, StockItemDto product)
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

            case Conflict:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.MovementEditor_Changed);
                await ReloadProductAsync(product);
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

    /// <summary>Recarga la existencia del producto tras un conflicto, conservando lo capturado.</summary>
    private async Task ReloadProductAsync(StockItemDto product)
    {
        var (completed, result) = await _runner.RunAsync(
            "RecargarExistencia",
            () => _useCases.RunAsync<SearchStockHandler, Result<StockPage>>(h => h.HandleAsync(
                new SearchStockQuery(product.Sku, StockFilter.All, IncludeInactive: true), CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = product.ProductId });
        if (completed && result is { IsSuccess: true }
            && result.Value.Items.FirstOrDefault(i => i.ProductId == product.ProductId) is { } fresh)
        {
            SetProduct(fresh);
        }
    }

    private void SetError(string field, string message)
    {
        switch (field)
        {
            case InventoryFields.ProductId:
                ProductError = message;
                break;
            case InventoryFields.Type:
                TypeError = message;
                break;
            case InventoryFields.Quantity:
                QuantityError = message;
                break;
            case InventoryFields.Reason:
                ReasonError = message;
                break;
            case InventoryFields.Reference:
                ReferenceError = message;
                break;
        }
    }

    private void ClearErrors()
    {
        ProductError = TypeError = QuantityError = ReasonError = ReferenceError = null;
        FocusField = null;
    }

    /// <summary>Mensaje de confirmación tras guardar, con la existencia resultante y su unidad.</summary>
    public static string RegisteredMessage(MovementDto movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return string.Format(
            CultureInfo.CurrentCulture,
            Strings.MovementEditor_Registered,
            QuantityConverter.Format(movement.ResultingStockThousandths, movement.DecimalPlaces),
            movement.UnitName);
    }
}

/// <summary>Estado normalizado del formulario de movimiento, para detectar cambios.</summary>
internal sealed record MovementFormState(Guid? ProductId, MovementType? Type, string Quantity, string Reason, string Reference);
