using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Settings;

public partial class DrawerReasonView : UserControl
{
    public DrawerReasonView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ReasonBox.Focus());
    }
}
