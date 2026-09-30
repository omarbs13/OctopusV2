using Avalonia.Controls;
using Avalonia.Threading;

namespace Pos.Desktop.Auth;

public partial class LoginView : UserControl
{
    private LoginViewModel? _viewModel;

    public LoginView()
    {
        InitializeComponent();

        // Foco en Usuario, o en Contraseña si el usuario ya viene lleno.
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (string.IsNullOrEmpty(_viewModel?.UserName))
            {
                UserNameBox.Focus();
            }
            else
            {
                PasswordBox.Focus();
            }
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested -= OnFocusPassword;
            _viewModel.FocusUserNameRequested -= OnFocusUserName;
        }

        _viewModel = DataContext as LoginViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusPasswordRequested += OnFocusPassword;
            _viewModel.FocusUserNameRequested += OnFocusUserName;
        }

        base.OnDataContextChanged(e);
    }

    private void OnFocusPassword(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => PasswordBox.Focus());

    private void OnFocusUserName(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => UserNameBox.Focus());
}
