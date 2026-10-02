using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Reports;

public partial class PurchaseReportView : UserControl
{
    public PurchaseReportView() => InitializeComponent();

    /// <summary>Doble clic en una fila abre el detalle de la compra.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PurchaseReportViewModel page && page.OpenPurchaseCommand.CanExecute(null))
        {
            page.OpenPurchaseCommand.Execute(null);
        }
    }
}
