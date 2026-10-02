using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Purchases;

public partial class SupplierListView : UserControl
{
    public SupplierListView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    /// <summary>Doble clic en una fila edita el proveedor.</summary>
    private void OnSupplierDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SupplierListViewModel page && page.EditCommand.CanExecute(null))
        {
            page.EditCommand.Execute(null);
        }
    }
}
