using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Sales;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Sales;

/// <summary>Fila del selector de productos con los textos ya formateados.</summary>
public sealed record ProductChooserRow(SaleProductDto Item)
{
    public string Name => Item.Name;

    public string Sku => Item.Sku;

    public string PriceText => MoneyConverter.Format(Item.PriceCents);

    public string StockText => Item.OnHandThousandths is { } onHand
        ? $"{QuantityConverter.Format(onHand, Item.DecimalPlaces)} {Item.UnitName}"
        : QuantityConverter.NoValue;

    public string StatusText => Item.NotSellableReason switch
    {
        NotSellableReason.Inactive => Strings.Sale_ProductInactive,
        NotSellableReason.Deleted => Strings.Sale_ProductDeleted,
        _ => string.Empty,
    };
}

/// <summary>Lista de resultados de una búsqueda por nombre: ↑/↓ mueven, Enter elige y Esc cierra.</summary>
public sealed partial class ProductChooserViewModel : ViewModelBase
{
    private readonly Action<SaleProductDto> _onChosen;
    private readonly Action _onClosed;

    public ProductChooserViewModel(IReadOnlyList<SaleProductDto> items, Action<SaleProductDto> onChosen, Action onClosed)
    {
        ArgumentNullException.ThrowIfNull(items);
        _onChosen = onChosen;
        _onClosed = onClosed;
        Rows = [.. items.Select(i => new ProductChooserRow(i))];
        SelectedRow = Rows.Count > 0 ? Rows[0] : null;
    }

    public IReadOnlyList<ProductChooserRow> Rows { get; }

    [ObservableProperty]
    public partial ProductChooserRow? SelectedRow { get; set; }

    [RelayCommand]
    private void Choose()
    {
        if (SelectedRow is { } row)
        {
            _onChosen(row.Item);
        }
    }

    [RelayCommand]
    private void Close() => _onClosed();
}
