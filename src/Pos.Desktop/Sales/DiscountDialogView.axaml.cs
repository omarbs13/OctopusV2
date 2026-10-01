using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class DiscountDialogView : UserControl
{
    public DiscountDialogView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        });
    }
}
