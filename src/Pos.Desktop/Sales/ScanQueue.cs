using System.Threading.Channels;

namespace Pos.Desktop.Sales;

/// <summary>
/// Cola de lecturas del campo de captura (research §8): un solo consumidor las atiende en orden. El
/// texto se toma y se limpia de forma síncrona en el evento de tecla, así que las lecturas seguidas
/// de un lector de códigos no se mezclan. El consumidor se inicia en el contexto de la interfaz, por
/// lo que el manejador puede tocar los controles sin marshaling.
/// </summary>
#pragma warning disable CA1711 // El nombre describe su función: una cola de lecturas.
public sealed class ScanQueue : IDisposable
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Func<string, Task> _handler;
    private readonly Action<Exception> _onError;

    public ScanQueue(Func<string, Task> handler, Action<Exception> onError)
    {
        _handler = handler;
        _onError = onError;
    }

    public void Start() => _ = ConsumeAsync();

    public void Enqueue(string text)
    {
        _channel.Writer.TryWrite(text);
    }

    public void Dispose() => _channel.Writer.TryComplete();

    private async Task ConsumeAsync()
    {
        await foreach (var text in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await _handler(text);
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
