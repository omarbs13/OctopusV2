using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pos.Application.Categories;
using Pos.Application.Products;

namespace Pos.Desktop.Products;

public partial class ProductEditorView : UserControl
{
    private ProductEditorViewModel? _viewModel;

    public ProductEditorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => NameBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ProductEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error (FR-014).</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProductEditorViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        if (field == ProductFields.UnitCode)
        {
            UnitBox.Focus();
            return;
        }

        if (field == CategoryFields.Category)
        {
            CategoryBox.FocusSelector();
            return;
        }

        if (field == ProductFields.TracksInventory)
        {
            TracksInventoryBox.Focus();
            return;
        }

        TextBox target = field switch
        {
            ProductFields.Sku => SkuBox,
            ProductFields.Barcode => BarcodeBox,
            ProductFields.Price => PriceBox,
            ProductFields.MinimumStock => MinimumStockBox,
            _ => NameBox,
        };
        target.Focus();
        target.SelectAll();
    }

    /// <summary>Muestra el SKU en mayúsculas al salir del campo, como se guardará.</summary>
    private void OnSkuLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Sku = _viewModel.Sku.ToUpperInvariant();
        }
    }
}
