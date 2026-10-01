using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class CouponEntryView : UserControl
{
    public CouponEntryView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => CodeBox.Focus());
    }
}
