using Microsoft.Extensions.Logging;
using Pos.Application.Printing;
using Pos.Application.Printing.Ticket;

namespace Pos.Infrastructure.Printing;

/// <summary>
/// Impresora de tickets: virtual (archivo) o física (ESC/POS por el transporte del sistema). Toda
/// excepción se convierte en un <see cref="PrintOutcome"/>; nunca llega a la interfaz.
/// </summary>
public sealed partial class PlatformTicketPrinter : ITicketPrinter
{
    private readonly FileTicketPrinter _file;
    private readonly IRawPrinterTransport _transport;
    private readonly PrintGate _gate;
    private readonly ILogger<PlatformTicketPrinter> _logger;

    public PlatformTicketPrinter(
        FileTicketPrinter file,
        IRawPrinterTransport transport,
        PrintGate gate,
        ILogger<PlatformTicketPrinter> logger)
    {
        _file = file;
        _transport = transport;
        _gate = gate;
        _logger = logger;
    }

    public async Task<PrintOutcome> PrintAsync(TicketDocument ticket, PrintingSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (settings.UseVirtualPrinter)
            {
                return await _gate.RunAsync(() => _file.PrintAsync(ticket, cancellationToken), cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                return PrintOutcome.Fail(DeviceFailure.NotConfigured);
            }

            var bytes = EscPosEncoder.Encode(ticket, settings.LogoMaxDots);
            var failure = await _gate.RunAsync(() => _transport.SendAsync(settings.PrinterName, bytes, cancellationToken), cancellationToken);
            return failure is null
                ? PrintOutcome.Ok(settings.PrinterName)
                : PrintOutcome.Fail(failure.Value, settings.PrinterName);
        }
#pragma warning disable CA1031 // Ninguna falla de dispositivo llega a la interfaz.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(ex, settings.PrinterName);
            return PrintOutcome.Fail(DeviceFailure.IoError, settings.PrinterName);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falla inesperada al imprimir. Impresora={Printer}")]
    private partial void LogFailed(Exception exception, string? printer);
}

/// <summary>Cajón de dinero: pulso ESC/POS por el mismo transporte; con impresora virtual solo se simula.</summary>
public sealed partial class PlatformCashDrawer : ICashDrawer
{
    private readonly IRawPrinterTransport _transport;
    private readonly PrintGate _gate;
    private readonly ILogger<PlatformCashDrawer> _logger;

    public PlatformCashDrawer(IRawPrinterTransport transport, PrintGate gate, ILogger<PlatformCashDrawer> logger)
    {
        _transport = transport;
        _gate = gate;
        _logger = logger;
    }

    public async Task<DrawerOutcome> OpenAsync(PrintingSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (settings.UseVirtualPrinter)
            {
                LogSimulated();
                return DrawerOutcome.Ok("Impresora virtual", simulated: true);
            }

            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                return DrawerOutcome.Fail(DeviceFailure.NotConfigured);
            }

            var failure = await _gate.RunAsync(
                () => _transport.SendAsync(settings.PrinterName, EscPosEncoder.DrawerPulse(), cancellationToken),
                cancellationToken);
            return failure is null
                ? DrawerOutcome.Ok(settings.PrinterName)
                : DrawerOutcome.Fail(failure.Value, settings.PrinterName);
        }
#pragma warning disable CA1031 // Ninguna falla de dispositivo llega a la interfaz.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(ex, settings.PrinterName);
            return DrawerOutcome.Fail(DeviceFailure.IoError, settings.PrinterName);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Apertura de cajón simulada (impresora virtual)")]
    private partial void LogSimulated();

    [LoggerMessage(Level = LogLevel.Error, Message = "Falla inesperada al abrir el cajón. Impresora={Printer}")]
    private partial void LogFailed(Exception exception, string? printer);
}

/// <summary>Impresoras instaladas en el sistema operativo.</summary>
public sealed class PlatformPrinterCatalog : IPrinterCatalog
{
    private readonly IRawPrinterTransport _transport;

    public PlatformPrinterCatalog(IRawPrinterTransport transport) => _transport = transport;

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) =>
        _transport.ListPrintersAsync(cancellationToken);
}
