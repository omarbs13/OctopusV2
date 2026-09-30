using Pos.Application.Printing;

namespace Pos.Infrastructure.Printing;

/// <summary>Envío de bytes crudos a una impresora del sistema operativo (006, research §5).</summary>
public interface IRawPrinterTransport
{
    Task<IReadOnlyList<string>> ListPrintersAsync(CancellationToken cancellationToken);

    /// <summary>Envía los bytes como un solo trabajo; nulo si se envió, o el motivo de la falla. No lanza.</summary>
    Task<DeviceFailure?> SendAsync(string printerName, byte[] data, CancellationToken cancellationToken);
}
