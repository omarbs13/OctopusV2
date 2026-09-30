using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Administration;

public partial class ResetPasswordView : UserControl
{
    public ResetPasswordView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => PasswordBox.Focus());
    }
}
