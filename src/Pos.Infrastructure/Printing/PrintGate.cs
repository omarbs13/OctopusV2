namespace Pos.Infrastructure.Printing;

/// <summary>Serializa los trabajos de impresión: un ticket es un solo trabajo y dos no se mezclan.</summary>
public sealed class PrintGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await job();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
