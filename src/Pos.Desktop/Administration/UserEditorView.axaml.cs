using System.ComponentModel;
using Avalonia.Controls;
using Pos.Application.Users;

namespace Pos.Desktop.Administration;

public partial class UserEditorView : UserControl
{
    private UserEditorViewModel? _viewModel;

    public UserEditorView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => FullNameBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as UserEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UserEditorViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        if (field == UserFields.Role)
        {
            RoleBox.Focus();
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
