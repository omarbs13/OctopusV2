using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Categories;

public partial class CategoriesView : UserControl
{
    public CategoriesView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    /// <summary>Doble clic en una fila abre la categoría para editarla.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CategoriesViewModel page && page.EditCommand.CanExecute(null))
        {
            page.EditCommand.Execute(null);
        }
    }
}
