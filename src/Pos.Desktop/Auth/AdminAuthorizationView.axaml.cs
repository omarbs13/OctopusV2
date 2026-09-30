using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Auth;

public partial class AdminAuthorizationView : UserControl
{
    private AdminAuthorizationViewModel? _viewModel;

    public AdminAuthorizationView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => UserNameBox.Focus());
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested -= OnFocusPassword;
        }

        _viewModel = DataContext as AdminAuthorizationViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested += OnFocusPassword;
        }

        base.OnDataContextChanged(e);
    }

    private void OnFocusPassword(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => PasswordBox.Focus());
}
