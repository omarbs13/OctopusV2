using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;

namespace Pos.Desktop.Purchases;

/// <summary>
/// Línea de la captura de una compra. Solo guarda lo capturado y muestra el importe que devuelve
/// <c>CalculatePurchaseTotals</c>; no calcula nada (Principio III).
/// </summary>
public sealed partial class PurchaseLineViewModel : ObservableObject
{
    private readonly Action<PurchaseLineViewModel> _changed;
    private readonly Action<PurchaseLineViewModel> _remove;

    public PurchaseLineViewModel(
        Guid productId,
        string name,
        string sku,
        string unitName,
        int decimalPlaces,
        Action<PurchaseLineViewModel> changed,
        Action<PurchaseLineViewModel> remove)
    {
        ProductId = productId;
        Name = name;
        Sku = sku;
        UnitName = unitName;
        DecimalPlaces = decimalPlaces;
        _changed = changed;
        _remove = remove;
    }

    public Guid ProductId { get; }

    public string Name { get; }

    public string Sku { get; }

    public string UnitName { get; }

    public int DecimalPlaces { get; }

    [ObservableProperty]
    public partial int Number { get; set; }

    [ObservableProperty]
    public partial string QuantityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnitCostText { get; set; } = string.Empty;

    /// <summary>Importe calculado por el caso de uso; vacío mientras la línea tiene errores.</summary>
    [ObservableProperty]
    public partial string AmountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBonus { get; private set; }

    [ObservableProperty]
    public partial string? ProductError { get; set; }

    [ObservableProperty]
    public partial string? QuantityError { get; set; }

    [ObservableProperty]
    public partial string? CostError { get; set; }

    /// <summary>Aplica el resultado del cálculo de esta línea.</summary>
    public void ApplyTotals(long? amountCents, bool isBonus)
    {
        AmountText = amountCents is { } cents ? MoneyConverter.Format(cents) : string.Empty;
        IsBonus = isBonus && amountCents is not null;
    }

    public void ClearErrors() => ProductError = QuantityError = CostError = null;

    partial void OnQuantityTextChanged(string value) => _changed(this);

    partial void OnUnitCostTextChanged(string value) => _changed(this);

    [RelayCommand]
    private void Remove() => _remove(this);
}
