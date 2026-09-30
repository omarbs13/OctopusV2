using Pos.Application.Printing.Ticket;

namespace Pos.Application.Printing;

public interface ITicketPrinter
{
    /// <summary>Imprime o guarda el ticket según la configuración vigente; no lanza por fallas del dispositivo.</summary>
    Task<PrintOutcome> PrintAsync(TicketDocument ticket, PrintingSettings settings, CancellationToken cancellationToken);
}
