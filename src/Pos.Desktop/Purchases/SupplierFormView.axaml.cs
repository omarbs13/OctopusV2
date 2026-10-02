using System.ComponentModel;
using Avalonia.Controls;
using Pos.Application.Suppliers;

namespace Pos.Desktop.Purchases;

public partial class SupplierFormView : UserControl
{
    private SupplierFormViewModel? _viewModel;

    public SupplierFormView()
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

        _viewModel = DataContext as SupplierFormViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Lleva el foco al primer campo con error.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SupplierFormViewModel.FocusField) || _viewModel?.FocusField is not { } field)
        {
            return;
        }

        if (field == SupplierFields.PaymentTerms)
        {
            TermsBox.Focus();
            return;
        }

        TextBox target = field switch
        {
            SupplierFields.TaxId => TaxIdBox,
            SupplierFields.Phone => PhoneBox,
            SupplierFields.Email => EmailBox,
            SupplierFields.Address => AddressBox,
            SupplierFields.CreditDays => CreditDaysBox,
            _ => NameBox,
        };
        target.Focus();
        target.SelectAll();
    }
}
