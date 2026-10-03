using Pos.Application.Business;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Encabezado del negocio común a todos los tickets (023, FR-022): las líneas de
/// <see cref="BusinessHeader.Lines"/> centradas y ajustadas al ancho, con el nombre en negrita. El logo
/// sigue en <see cref="TicketDocument.Logo"/>.
/// </summary>
public static class TicketHeader
{
    /// <summary>Agrega el encabezado; sin datos del negocio no agrega nada y el ticket empieza en su título.</summary>
    public static void Add(List<TicketLine> lines, BusinessProfileDto? profile, int columns)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (BusinessHeader.From(profile) is not { } header)
        {
            return;
        }

        foreach (var line in header.Lines)
        {
            lines.AddRange(TextWrap.Wrap(line.Text, columns).Select(t => new TicketLine(t, TicketAlignment.Center, Bold: line.IsTitle)));
        }
    }
}
