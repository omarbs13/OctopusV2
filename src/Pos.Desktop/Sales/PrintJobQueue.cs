using System.Threading.Channels;

namespace Pos.Desktop.Sales;

/// <summary>
/// Cola serial de trabajos de impresión y cajón (006, research §7): un solo consumidor los atiende
/// en el orden en que se registraron las ventas, así que los tickets rápidos no se reordenan antes
/// de llegar a <c>PrintGate</c>. No persiste nada ni reintenta por su cuenta. Como
/// <see cref="ScanQueue"/>, se inicia en el contexto de la interfaz, por lo que un trabajo puede
/// mostrar avisos sin marshaling; la E/S de la impresora es asíncrona y no bloquea la interfaz.
/// </summary>
#pragma warning disable CA1711 // El nombre describe su función: una cola de trabajos.
public sealed class PrintJobQueue : IDisposable
#pragma warning restore CA1711
{
    private readonly Channel<Func<Task>> _channel = Channel.CreateUnbounded<Func<Task>>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Action<Exception> _onError;
    private bool _started;

    public PrintJobQueue(Action<Exception> onError) => _onError = onError;

    /// <summary>Encola el trabajo; en la primera llamada inicia al consumidor en el contexto actual.</summary>
    public void Enqueue(Func<Task> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!_started)
        {
            _started = true;
            _ = ConsumeAsync();
        }

        _channel.Writer.TryWrite(job);
    }

    public void Dispose() => _channel.Writer.TryComplete();

    private async Task ConsumeAsync()
    {
        await foreach (var job in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await job();
            }
#pragma warning disable CA1031 // Un trabajo fallido no debe detener a los siguientes ni afectar la venta.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _onError(ex);
            }
        }
    }
}
