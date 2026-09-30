using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Products;

public partial class ProductsView : UserControl
{
    public ProductsView()
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
    }

    /// <summary>Doble clic en una fila abre el editor (FR-019).</summary>
    private void OnProductDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProductsViewModel page && page.EditCommand.CanExecute(null))
        {
            page.EditCommand.Execute(null);
        }
    }
}
