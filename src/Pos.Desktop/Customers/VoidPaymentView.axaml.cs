using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Customers;

public partial class VoidPaymentView : UserControl
{
    public VoidPaymentView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ReasonBox.Focus());
    }
}
