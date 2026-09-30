using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.CashShifts;

public partial class OpenShiftView : UserControl
{
    public OpenShiftView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => FloatBox.Focus());
    }
}
