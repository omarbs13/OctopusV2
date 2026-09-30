using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class CancelSaleView : UserControl
{
    public CancelSaleView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ReasonBox.Focus());
    }
}
