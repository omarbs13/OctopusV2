using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pos.Desktop.Common.Scanner;

namespace Pos.Desktop.Settings;

/// <summary>
/// Teclado de la pantalla "Probar escáner" (021, research §10): no hay campo de texto; la vista toma el foco
/// y arma cada lectura con su propio detector. Enter y Tab la cierran (Tab no mueve el foco mientras forma
/// parte de una lectura) y un silencio de 300 ms cierra la que no trajo terminador (FR-017).
/// </summary>
public partial class ScannerTestView : UserControl
{
    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly ScanBurstDetector _detector = new();
    private readonly DispatcherTimer _idleTimer;

    public ScannerTestView()
    {
        InitializeComponent();
        AddHandler(TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _idleTimer = new DispatcherTimer { Interval = IdleCheckInterval };
        _idleTimer.Tick += OnIdleTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _idleTimer.Start();
        Dispatcher.UIThread.Post(() => Focus());
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _idleTimer.Stop();
        _detector.Reset();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Un clic en la página devuelve el foco a la vista para seguir leyendo.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        _detector.OnText(e.Text, Stopwatch.GetTimestamp(), null);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var terminator = e.Key switch
        {
            Key.Enter => Terminator.Enter,
            Key.Tab when _detector.HasPending => Terminator.Tab,
            _ => (Terminator?)null,
        };
        if (terminator is not { } value)
        {
            return;
        }

        e.Handled = true;
        Deliver(_detector.OnTerminator(value, Stopwatch.GetTimestamp()));
    }

    private void OnIdleTick(object? sender, EventArgs e) => Deliver(_detector.CheckIdle(Stopwatch.GetTimestamp()));

    private void Deliver(ScanReading? reading)
    {
        if (reading is not null && DataContext is ScannerTestViewModel vm)
        {
            _ = vm.ReceiveAsync(reading);
        }
    }
}
