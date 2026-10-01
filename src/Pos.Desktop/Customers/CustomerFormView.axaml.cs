using System.ComponentModel;
using Avalonia.Controls;
using Pos.Application.Customers;

namespace Pos.Desktop.Customers;

public partial class CustomerFormView : UserControl
{
    private CustomerFormViewModel? _viewModel;

    public CustomerFormView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => NameBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as CustomerFormViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CustomerFormViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        if (field == CustomerFields.CreditMode)
        {
            ModeBox.Focus();
            return;
        }

        TextBox target = field switch
        {
            CustomerFields.Phone => PhoneBox,
            CustomerFields.Email => EmailBox,
            CustomerFields.TaxId => TaxIdBox,
            CustomerFields.CreditLimit => LimitBox,
            _ => NameBox,
        };
        target.Focus();
        target.SelectAll();
    }
}
