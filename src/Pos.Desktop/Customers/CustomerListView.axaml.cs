using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Customers;

public partial class CustomerListView : UserControl
{
    public CustomerListView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    /// <summary>Doble clic en una fila abre la ficha del cliente.</summary>
    private void OnCustomerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CustomerListViewModel page && page.OpenDetailCommand.CanExecute(null))
        {
            page.OpenDetailCommand.Execute(null);
        }
    }
}
