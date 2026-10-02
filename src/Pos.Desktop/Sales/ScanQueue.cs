using System.Threading.Channels;

namespace Pos.Desktop.Sales;

/// <summary>Lectura del campo de captura: el texto y si llegó como ráfaga del escáner (021).</summary>
public sealed record ScanInput(string Text, bool IsScan);

/// <summary>
/// Cola de lecturas del campo de captura (research §8): un solo consumidor las atiende en orden. El
/// texto se toma y se limpia de forma síncrona en el evento de tecla, así que las lecturas seguidas
/// de un lector de códigos no se mezclan. El consumidor se inicia en el contexto de la interfaz, por
/// lo que el manejador puede tocar los controles sin marshaling.
/// </summary>
#pragma warning disable CA1711 // El nombre describe su función: una cola de lecturas.
public sealed class ScanQueue : IDisposable
{
    private readonly Channel<ScanInput> _channel = Channel.CreateUnbounded<ScanInput>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Func<ScanInput, Task> _handler;
    private readonly Action<Exception> _onError;

    public ScanQueue(Func<ScanInput, Task> handler, Action<Exception> onError)
    {
        _handler = handler;
        _onError = onError;
    }

    public void Start() => _ = ConsumeAsync();

    public void Enqueue(ScanInput input)
    {
        _channel.Writer.TryWrite(input);
    }

    public void Dispose() => _channel.Writer.TryComplete();

    private async Task ConsumeAsync()
    {
        await foreach (var input in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await _handler(input);
            }
#pragma warning disable CA1031 // Una lectura fallida no debe detener a las siguientes.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _onError(ex);
            }
        }
    }
}
#pragma warning restore CA1711
