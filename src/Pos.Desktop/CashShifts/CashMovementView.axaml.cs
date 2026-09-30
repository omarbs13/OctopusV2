using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.CashShifts;

public partial class CashMovementView : UserControl
{
    public CashMovementView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => AmountBox.Focus());
    }
}
