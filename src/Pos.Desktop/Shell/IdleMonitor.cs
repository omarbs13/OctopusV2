using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Pos.Desktop.Shell;

/// <summary>
/// Detecta la inactividad de la sesión (FR-023). Escucha teclado y puntero de la ventana principal
/// con manejadores de túnel y, con <see cref="Touch"/>, la actividad de los diálogos. Un temporizador
/// compara la última actividad con el tiempo configurado; 0 minutos desactiva el bloqueo.
/// </summary>
public sealed class IdleMonitor : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private DispatcherTimer? _timer;
    private TimeSpan _limit;
    private long _lastActivityTicks = Environment.TickCount64;

    /// <summary>Se cumplió el tiempo sin actividad; el monitor se pausa hasta <see cref="Resume"/>.</summary>
    public event EventHandler? IdleElapsed;

    /// <summary>Registra actividad del operador.</summary>
    public void Touch() => Interlocked.Exchange(ref _lastActivityTicks, Environment.TickCount64);

    /// <summary>Escucha la actividad de una ventana, incluso si otro control ya manejó el evento.</summary>
    public void Attach(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        void OnActivity(object? sender, RoutedEventArgs e) => Touch();
        window.AddHandler(InputElement.KeyDownEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerPressedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerMovedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerWheelChangedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Empieza a vigilar; <paramref name="minutes"/> = 0 desactiva el bloqueo.</summary>
    public void Start(int minutes)
    {
        Stop();
        if (minutes <= 0)
        {
            return;
        }

        _limit = TimeSpan.FromMinutes(minutes);
        Touch();
        StartTimer();
    }

    /// <summary>Reanuda tras desbloquear, con el tiempo vigente.</summary>
    public void Resume()
    {
        if (_limit <= TimeSpan.Zero)
        {
            return;
        }

        Touch();
        StartTimer();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _limit = TimeSpan.Zero;
    }

    public void Dispose() => Stop();

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer(CheckInterval, DispatcherPriority.Background, OnTick);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastActivityTicks));
        if (_limit <= TimeSpan.Zero || elapsed < _limit)
        {
            return;
        }

        _timer?.Stop();
        IdleElapsed?.Invoke(this, EventArgs.Empty);
    }
}
