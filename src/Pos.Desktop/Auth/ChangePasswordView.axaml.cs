using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Pos.Application.Users;

namespace Pos.Desktop.Auth;

public partial class ChangePasswordView : UserControl
{
    private ChangePasswordViewModel? _viewModel;

    public ChangePasswordView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel?.AsksCurrentPassword == true)
            {
                CurrentBox.Focus();
            }
            else
            {
                NewBox.Focus();
            }
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ChangePasswordViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChangePasswordViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        TextBox target = field switch
        {
            UserFields.CurrentPassword => CurrentBox,
            UserFields.ConfirmPassword => ConfirmBox,
            _ => NewBox,
        };
        target.Focus();
        target.SelectAll();
    }
}
