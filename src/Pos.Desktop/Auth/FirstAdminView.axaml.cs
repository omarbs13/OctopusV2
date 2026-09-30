using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Pos.Application.Users;

namespace Pos.Desktop.Auth;

public partial class FirstAdminView : UserControl
{
    private FirstAdminViewModel? _viewModel;

    public FirstAdminView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => FullNameBox.Focus());
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as FirstAdminViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FirstAdminViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        TextBox target = field switch
        {
            UserFields.UserName => UserNameBox,
            UserFields.Password => PasswordBox,
            UserFields.ConfirmPassword => ConfirmPasswordBox,
            _ => FullNameBox,
        };
        target.Focus();
        target.SelectAll();
    }
}
