using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Customers;

public partial class RegisterPaymentView : UserControl
{
    public RegisterPaymentView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => AmountBox.Focus());
    }
}
