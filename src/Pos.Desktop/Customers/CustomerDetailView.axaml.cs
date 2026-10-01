using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Customers;

public partial class CustomerDetailView : UserControl
{
    public CustomerDetailView() => InitializeComponent();

    /// <summary>Doble clic en una venta a crédito abre su detalle.</summary>
    private void OnReceivableDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CustomerDetailViewModel detail && detail.OpenSaleCommand.CanExecute(null))
        {
            detail.OpenSaleCommand.Execute(null);
        }
    }
}
