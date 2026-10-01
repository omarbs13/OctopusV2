using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Desktop.Sales;

/// <summary>Opción del selector de modalidad del descuento.</summary>
public sealed record DiscountModeOption(DiscountMode Mode, string Label);

/// <summary>
/// Diálogo de descuento de una línea (F7) o de la venta (Shift+F7) (015, contracts/ui.md): modalidad, valor
/// y vista previa del importe final. Los cálculos los hace el dominio (<see cref="DiscountValue"/>,
/// <see cref="DiscountMath"/>); la autorización y la aplicación las decide el Punto de venta.
/// </summary>
public sealed partial class DiscountDialogViewModel : ViewModelBase
{
    private readonly Func<DiscountValue, Task<string?>> _apply;
    private readonly Action? _remove;
    private readonly Action _close;

    /// <param name="title">Título: línea o venta.</param>
    /// <param name="baseCents">Importe de la línea o subtotal de la venta.</param>
    /// <param name="current">Descuento actual, para editarlo.</param>
    /// <param name="apply">Aplica el descuento; devuelve el error o nulo si se aplicó.</param>
    /// <param name="remove">Quita el descuento actual; nulo si no hay.</param>
    /// <param name="close">Cierra el diálogo sin cambios.</param>
    public DiscountDialogViewModel(
        string title,
        long baseCents,
        DiscountValue? current,
        Func<DiscountValue, Task<string?>> apply,
        Action? remove,
        Action close)
    {
        Title = title;
        BaseCents = baseCents;
        _apply = apply;
        _remove = remove;
        _close = close;
        Modes =
        [
            new DiscountModeOption(DiscountMode.Percent, Strings.Discount_ModePercent),
            new DiscountModeOption(DiscountMode.Amount, Strings.Discount_ModeAmount),
        ];
        SelectedMode = Modes.First(m => m.Mode == (current?.Mode ?? DiscountMode.Percent));
        ValueText = current?.ToEditableString() ?? string.Empty;
    }

    public string Title { get; }

    public long BaseCents { get; }

    public string BaseText => string.Format(MoneyConverter.Culture, Strings.Discount_BaseLabel, MoneyConverter.Format(BaseCents));

    public IReadOnlyList<DiscountModeOption> Modes { get; }

    public bool CanRemove => _remove is not null;

    [ObservableProperty]
    public partial DiscountModeOption SelectedMode { get; set; }

    [ObservableProperty]
    public partial string ValueText { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsBusy { get; private set; }

    partial void OnSelectedModeChanged(DiscountModeOption value) => UpdatePreview();

    partial void OnValueTextChanged(string value) => UpdatePreview();

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        ErrorMessage = null;
        DiscountValue value;
        try
        {
            value = DiscountValue.Parse(SelectedMode.Mode, ValueText);
            _ = DiscountMath.Amount(BaseCents, value);
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        IsBusy = true;
        try
        {
            ErrorMessage = await _apply(value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Remove() => _remove?.Invoke();

    [RelayCommand]
    private void Close() => _close();

    private bool CanApply() => !IsBusy;

    /// <summary>Importe final que resultaría; vacío mientras el valor no es válido.</summary>
    private void UpdatePreview()
    {
        try
        {
            var value = DiscountValue.Parse(SelectedMode.Mode, ValueText);
            var amount = DiscountMath.Amount(BaseCents, value);
            PreviewText = string.Format(MoneyConverter.Culture, Strings.Discount_Preview, MoneyConverter.Format(BaseCents - amount));
        }
        catch (DomainException)
        {
            PreviewText = string.Empty;
        }
    }
}
