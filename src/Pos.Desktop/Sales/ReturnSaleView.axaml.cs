using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class ReturnSaleView : UserControl
{
    public ReturnSaleView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ReasonBox.Focus());
    }
}
