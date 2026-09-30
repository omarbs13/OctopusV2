using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia;
using Pos.Domain.Sales;

namespace Pos.Desktop.Sales;

public partial class CheckoutView : UserControl
{
    private CheckoutViewModel? _viewModel;

    public CheckoutView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Solo se suscribe al ViewModel mientras la vista está en pantalla (el cobro pendiente se reutiliza).</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewModel = DataContext as CheckoutViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusReceivedRequested += OnFocusReceivedRequested;
        }

        FocusReceived();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusReceivedRequested -= OnFocusReceivedRequested;
            _viewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnFocusReceivedRequested(object? sender, EventArgs e) => FocusReceived();

    private void FocusReceived() =>
        Dispatcher.UIThread.Post(() =>
        {
            ReceivedBox.Focus();
            ReceivedBox.SelectAll();
        });

    /// <summary>
    /// Atajos del cobro (contracts/ui.md): Enter o F12 confirman, Esc regresa a la venta, F5 y 1–6
    /// capturan el efectivo (fuera de los campos de texto) y Supr quita el pago seleccionado.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
        {
            return;
        }

        var inText = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;
        var inPaymentFields = AmountBox.IsKeyboardFocusWithin || ReferenceBox.IsKeyboardFocusWithin;

        switch (e.Key)
        {
            case Key.Escape:
                vm.CancelCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F12:
                Confirm(vm);
                e.Handled = true;
                break;

            case Key.Enter when inPaymentFields:
                vm.AddPaymentCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter when !PaymentsList.IsKeyboardFocusWithin && !MethodBox.IsKeyboardFocusWithin && !IsButtonFocused():
                Confirm(vm);
                e.Handled = true;
                break;

            case Key.F5:
                vm.QuickAmountCommand.Execute("exact");
                e.Handled = true;
                break;

            case Key.Delete when !inText:
                vm.RemovePaymentCommand.Execute(null);
                e.Handled = true;
                break;

            case >= Key.D1 and <= Key.D6 when !inText && e.KeyModifiers == KeyModifiers.None:
                vm.QuickAmountCommand.Execute(Checkout.Bills[e.Key - Key.D1].ToString(System.Globalization.CultureInfo.InvariantCulture));
                e.Handled = true;
                break;

            case >= Key.NumPad1 and <= Key.NumPad6 when !inText:
                vm.QuickAmountCommand.Execute(Checkout.Bills[e.Key - Key.NumPad1].ToString(System.Globalization.CultureInfo.InvariantCulture));
                e.Handled = true;
                break;
        }
    }

    private static void Confirm(CheckoutViewModel vm)
    {
        if (vm.ConfirmCommand.CanExecute(null))
        {
            vm.ConfirmCommand.Execute(null);
        }
    }

    private bool IsButtonFocused() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Button;
}
