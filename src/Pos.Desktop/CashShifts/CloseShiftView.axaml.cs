using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.CashShifts;

public partial class CloseShiftView : UserControl
{
    public CloseShiftView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => CountedBox.Focus());
    }
}
