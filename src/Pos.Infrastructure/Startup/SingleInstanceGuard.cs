using System.IO.Pipes;
using System.Text;

namespace Pos.Infrastructure.Startup;

/// <summary>
/// Garantiza una sola instancia por usuario. El bloqueo exclusivo de <c>app.lock</c> lo libera el
/// sistema operativo si el proceso termina de forma abrupta. Una canalización con nombre permite
/// que una segunda instancia pida a la primera que traiga su ventana al frente.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string ActivateMessage = "activate";

    private readonly FileStream _lock;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listener;

    private SingleInstanceGuard(FileStream lockStream, string pipeName)
    {
        _lock = lockStream;
        _pipeName = pipeName;
        _listener = Task.Run(ListenAsync);
    }

    public event EventHandler? ActivationRequested;

    /// <summary>Nombre de canalización por usuario del sistema operativo.</summary>
    public static string DefaultPipeName =>
        "pos-" + new string([.. Environment.UserName.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

    /// <summary>Obtiene el bloqueo; devuelve nulo si otra instancia ya lo tiene.</summary>
    public static SingleInstanceGuard? TryAcquire(string lockFile, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFile);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockFile))!);
        try
        {
            var stream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstanceGuard(stream, pipeName);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Pide a la instancia existente que active su ventana. Nunca lanza.</summary>
    public static async Task<bool> TrySignalExistingAsync(string pipeName, TimeSpan timeout)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            using var cts = new CancellationTokenSource(timeout);
            await client.ConnectAsync(cts.Token);
            var bytes = Encoding.UTF8.GetBytes(ActivateMessage + "\n");
            await client.WriteAsync(bytes, cts.Token);
            await client.FlushAsync(cts.Token);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _listener.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
        _lock.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadLineAsync(_stop.Token);
                if (string.Equals(message, ActivateMessage, StringComparison.Ordinal))
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Conexión interrumpida: se vuelve a escuchar.
            }
        }
    }
}
