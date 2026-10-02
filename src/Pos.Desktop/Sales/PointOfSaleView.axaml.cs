using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia;
using Pos.Desktop.Common.Scanner;

namespace Pos.Desktop.Sales;

public partial class PointOfSaleView : UserControl
{
    private readonly ScanBurstDetector _detector = new();
    private readonly DispatcherTimer _starTimer;
    private PointOfSaleViewModel? _viewModel;
    private TopLevel? _topLevel;

    public PointOfSaleView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _starTimer = new DispatcherTimer { Interval = ScanBurstDetector.StarDeferral };
        _starTimer.Tick += OnStarTimerTick;
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
            _viewModel.AttachScanGuard();
        }

        // En la ventana y no en la vista: el diálogo de la sesión (autorización del Administrador) está
        // fuera de esta vista y sus lecturas también se ignoran (021, FR-008).
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(TextInputEvent, OnScannerTextInput, RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(KeyDownEvent, OnScannerKeyDown, RoutingStrategies.Tunnel);

        FocusCapture();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(TextInputEvent, OnScannerTextInput);
        _topLevel?.RemoveHandler(KeyDownEvent, OnScannerKeyDown);
        _topLevel = null;
        _starTimer.Stop();
        _detector.Reset();

        if (_viewModel is not null)
        {
            _viewModel.FocusCaptureRequested -= OnFocusCaptureRequested;
            _viewModel.FocusQuantityRequested -= OnFocusQuantityRequested;
            _viewModel.DetachScanGuard();
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
    /// Lectura con el foco fuera del campo de captura (021, research §5): sin diálogo abierto y con el foco
    /// fuera de un campo de texto, el carácter se escribe en el campo de captura, que toma el foco; los
    /// siguientes ya llegan ahí. Con el foco en otro campo de texto no se intercepta (FR-007). Con un diálogo
    /// abierto solo se alimenta el detector (FR-008).
    /// </summary>
    private void OnScannerTextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel is not { } vm || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var focused = ScanFocus.FocusedTextBox(this);
        _detector.OnText(e.Text, Stopwatch.GetTimestamp(), ScanFocus.Snapshot(focused));
        if (vm.IsDialogOpen)
        {
            return;
        }

        // Otro carácter después del "*": era el asterisco de una lectura CODE39, no el atajo.
        _starTimer.Stop();

        var inCapture = ReferenceEquals(focused, CaptureBox);
        if (focused is not null && !inCapture)
        {
            return;
        }

        var startsWithStar = e.Text == "*" && string.IsNullOrEmpty(CaptureBox.Text);
        if (!inCapture)
        {
            CaptureBox.Focus();
            CaptureBox.Text = (CaptureBox.Text ?? string.Empty) + e.Text;
            CaptureBox.CaretIndex = CaptureBox.Text.Length;
            e.Handled = true;
        }

        if (startsWithStar)
        {
            _starTimer.Start();
        }
    }

    /// <summary>
    /// Terminadores de lectura. Enter en el campo de captura encola el texto con su origen (escáner o manual).
    /// Con un diálogo abierto, el Enter que cierra una ráfaga no llega al diálogo, el campo enfocado recupera
    /// su texto y se avisa (FR-008). Un Enter sin texto previo conserva su efecto normal.
    /// </summary>
    private void OnScannerKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Tab)
        {
            _detector.OnTerminator(Terminator.Tab, Stopwatch.GetTimestamp());
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        var reading = _detector.OnTerminator(Terminator.Enter, Stopwatch.GetTimestamp());
        if (vm.IsDialogOpen)
        {
            if (reading is { IsBurst: true })
            {
                e.Handled = true;
                ScanFocus.Restore(this, reading.FocusedSnapshot);
                vm.ReportScanIgnoredInDialog();
            }

            return;
        }

        if (CaptureBox.IsKeyboardFocusWithin)
        {
            _starTimer.Stop();
            vm.Capture(isScan: reading is { IsBurst: true });
            e.Handled = true;
        }
    }

    /// <summary>
    /// Atajo <c>*</c> con el campo de captura vacío (research §9): si en 60 ms no llegó otro carácter, se
    /// quita el asterisco y se abre la cantidad.
    /// </summary>
    private void OnStarTimerTick(object? sender, EventArgs e)
    {
        _starTimer.Stop();
        if (_viewModel is { IsDialogOpen: false } vm && CaptureBox.Text == "*")
        {
            CaptureBox.Text = string.Empty;
            _detector.Reset();
            vm.ChangeQuantityCommand.Execute(null);
        }
    }

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

            // 015: F7 descuento de la línea; Shift+F7 descuento de la venta (contracts/ui.md).
            case Key.F7 when e.KeyModifiers == KeyModifiers.Shift:
                vm.OpenOrderDiscountCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F7:
                vm.OpenLineDiscountCommand.Execute(null);
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

            // "*" con el campo de captura vacío se atiende como texto (OnScannerTextInput y OnStarTimerTick).
        }
    }
}
