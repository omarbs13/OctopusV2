using Pos.Application.Printing;

namespace Pos.Infrastructure.Printing;

/// <summary>Sistema operativo sin transporte de impresión: no hay impresoras (queda la virtual).</summary>
public sealed class UnsupportedTransport : IRawPrinterTransport
{
    public Task<IReadOnlyList<string>> ListPrintersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<DeviceFailure?> SendAsync(string printerName, byte[] data, CancellationToken cancellationToken) =>
        Task.FromResult<DeviceFailure?>(DeviceFailure.Unavailable);
}
