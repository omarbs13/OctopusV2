using System.Collections.Concurrent;

namespace Pos.Desktop.Diagnostics;

/// <summary>Resumen de un error repetido dentro de su ventana, listo para escribirse en el log.</summary>
public sealed record ErrorSummary(string ExceptionType, int Repetitions, TimeSpan Window);

/// <summary>Qué hacer con una excepción recién observada.</summary>
public sealed record ErrorObservation(bool IsNew, int Count);

/// <summary>
/// Agrupa errores idénticos que se repiten en una ventana corta (FR-017): el primero se registra y se
/// muestra completo, las repeticiones solo se cuentan y se resumen en una sola entrada al cerrar la
/// ventana. También garantiza que el operador vea un solo mensaje a la vez.
/// </summary>
public sealed class ErrorEpisodeGate : IDisposable
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    private readonly Func<DateTimeOffset> _now;
    private readonly TimeSpan _window;
    private readonly Action<ErrorSummary> _onSummary;
    private readonly ConcurrentDictionary<string, Episode> _episodes = new();
    private readonly Timer? _timer;
    private int _notificationOpen;

    public ErrorEpisodeGate(Func<DateTimeOffset> now, TimeSpan window, Action<ErrorSummary> onSummary, bool autoClose = false)
    {
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(onSummary);
        _now = now;
        _window = window;
        _onSummary = onSummary;
        if (autoClose)
        {
            _timer = new Timer(_ => CloseExpired(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>Gate de producción: ventana de 5 s y cierre automático de episodios.</summary>
    public static ErrorEpisodeGate CreateDefault(Serilog.ILogger logger) =>
        new(
            () => DateTimeOffset.UtcNow,
            DefaultWindow,
            summary => logger.Fatal(
                "Error repetido {Repetitions} veces en {WindowSeconds} s: {ExceptionType}",
                summary.Repetitions,
                summary.Window.TotalSeconds,
                summary.ExceptionType),
            autoClose: true);

    /// <summary>Registra la observación; <c>IsNew</c> indica que es la primera del episodio.</summary>
    public ErrorObservation Observe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var key = KeyOf(exception);
        var now = _now();
        while (true)
        {
            if (_episodes.TryGetValue(key, out var current))
            {
                if (now - current.StartedAt <= _window)
                {
                    return new ErrorObservation(false, current.Increment());
                }

                if (_episodes.TryRemove(new KeyValuePair<string, Episode>(key, current)))
                {
                    Summarize(current);
                }

                continue;
            }

            if (_episodes.TryAdd(key, new Episode(exception.GetType().FullName ?? exception.GetType().Name, now)))
            {
                return new ErrorObservation(true, 1);
            }
        }
    }

    /// <summary>Cierra los episodios vencidos y escribe el resumen de los que tuvieron repeticiones.</summary>
    public void CloseExpired()
    {
        var now = _now();
        foreach (var (key, episode) in _episodes)
        {
            if (now - episode.StartedAt > _window && _episodes.TryRemove(new KeyValuePair<string, Episode>(key, episode)))
            {
                Summarize(episode);
            }
        }
    }

    /// <summary>Intenta abrir el aviso al operador; <c>false</c> si ya hay uno abierto.</summary>
    public bool TryBeginNotification() => Interlocked.CompareExchange(ref _notificationOpen, 1, 0) == 0;

    public void EndNotification() => Interlocked.Exchange(ref _notificationOpen, 0);

    public void Dispose() => _timer?.Dispose();

    private void Summarize(Episode episode)
    {
        if (episode.Count > 1)
        {
            try
            {
                _onSummary(new ErrorSummary(episode.ExceptionType, episode.Count, _window));
            }
#pragma warning disable CA1031 // El resumen es opcional; su falla no debe afectar a la aplicación.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }

    private static string KeyOf(Exception exception)
    {
        var firstFrame = exception.StackTrace?
            .Split('\n', 2, StringSplitOptions.TrimEntries)[0] ?? exception.Message;
        return $"{exception.GetType().FullName}|{firstFrame}";
    }

    private sealed class Episode
    {
        private int _count = 1;

        public Episode(string exceptionType, DateTimeOffset startedAt)
        {
            ExceptionType = exceptionType;
            StartedAt = startedAt;
        }

        public string ExceptionType { get; }

        public DateTimeOffset StartedAt { get; }

        public int Count => Volatile.Read(ref _count);

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
