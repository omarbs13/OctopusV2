using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Shell;

public partial class LockOverlayView : UserControl
{
    private LockViewModel? _viewModel;

    public LockOverlayView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => PasswordBox.Focus());
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested -= OnFocusPassword;
        }

        _viewModel = DataContext as LockViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested += OnFocusPassword;
        }

        base.OnDataContextChanged(e);
    }

    private void OnFocusPassword(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => PasswordBox.Focus());
}
