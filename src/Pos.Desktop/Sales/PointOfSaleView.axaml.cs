using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia;

namespace Pos.Desktop.Sales;

public partial class PointOfSaleView : UserControl
{
    private PointOfSaleViewModel? _viewModel;

    public PointOfSaleView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// El ViewModel es un singleton y la vista se crea en cada navegación: solo se suscribe mientras
    /// está en pantalla, para no acumular vistas viejas.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewModel = DataContext as PointOfSaleViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusCaptureRequested += OnFocusCaptureRequested;
            _viewModel.FocusQuantityRequested += OnFocusQuantityRequested;
        }

        FocusCapture();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusCaptureRequested -= OnFocusCaptureRequested;
            _viewModel.FocusQuantityRequested -= OnFocusQuantityRequested;
            _viewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnFocusCaptureRequested(object? sender, EventArgs e) => FocusCapture();

    private void OnFocusQuantityRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            QuantityBox.Focus();
            QuantityBox.SelectAll();
        });

    private void FocusCapture() => Dispatcher.UIThread.Post(() => CaptureBox.Focus());

    /// <summary>
    /// Atajos del Punto de venta (contracts/ui.md). Se atienden en la fase de túnel para que funcionen
    /// aunque el foco esté en el campo de captura; con una ventana modal abierta no aplican.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { IsModalOpen: false } vm)
        {
            return;
        }

        var captureEmpty = string.IsNullOrEmpty(CaptureBox.Text);
        var inQuantity = QuantityBox.IsKeyboardFocusWithin;

        switch (e.Key)
        {
            case Key.F2:
                vm.SearchCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F4:
                vm.ChangeQuantityCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F8:
                vm.CancelSaleCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F12:
                vm.CheckoutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete when captureEmpty && !inQuantity:
                vm.RemoveLineCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up when CaptureBox.IsKeyboardFocusWithin:
                vm.MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Down when CaptureBox.IsKeyboardFocusWithin:
                vm.MoveSelection(1);
                e.Handled = true;
                break;

            // "*" solo abre la cantidad con el campo de captura vacío; con texto es un carácter más.
            case Key.Multiply when captureEmpty && CaptureBox.IsKeyboardFocusWithin:
                vm.ChangeQuantityCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D8 when e.KeyModifiers == KeyModifiers.Shift && captureEmpty && CaptureBox.IsKeyboardFocusWithin:
                vm.ChangeQuantityCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
