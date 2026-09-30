using System.ComponentModel;
using Avalonia.Controls;
using Pos.Application.Inventory;

namespace Pos.Desktop.Inventory;

public partial class MovementEditorView : UserControl
{
    private MovementEditorViewModel? _viewModel;

    public MovementEditorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => QuantityBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MovementEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MovementEditorViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        TextBox target = field switch
        {
            InventoryFields.Reason => ReasonBox,
            InventoryFields.Reference => ReferenceBox,
            _ => QuantityBox,
        };
        target.Focus();
        target.SelectAll();
    }
}
