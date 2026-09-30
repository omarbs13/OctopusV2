using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Inventory;

public partial class StockView : UserControl
{
    public StockView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
        KeyDown += OnKeyDown;
    }

    /// <summary>Ctrl+F lleva el foco a la búsqueda.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && StockList.IsKeyboardFocusWithin)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    /// <summary>Doble clic en una fila abre el formulario de movimiento con el producto elegido.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (DataContext is StockViewModel page && page.RegisterMovementCommand.CanExecute(null))
        {
            page.RegisterMovementCommand.Execute(null);
        }
    }
}
