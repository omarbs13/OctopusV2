using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Purchases;

public partial class VoidPurchaseView : UserControl
{
    public VoidPurchaseView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ReasonBox.Focus());
    }
}
