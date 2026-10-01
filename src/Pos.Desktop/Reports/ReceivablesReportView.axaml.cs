using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Reports;

public partial class ReceivablesReportView : UserControl
{
    public ReceivablesReportView() => InitializeComponent();

    /// <summary>Doble clic en una fila abre la ficha del cliente.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ReceivablesReportViewModel page && page.OpenCustomerCommand.CanExecute(null))
        {
            page.OpenCustomerCommand.Execute(null);
        }
    }
}
