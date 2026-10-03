using System.Globalization;
using Pos.Application.Business;
using Pos.Application.CreditNotes;

namespace Pos.Application.Printing.Ticket;

/// <summary>
/// Arma el ticket de una nota de crédito: folio, saldo, fecha y venta de origen, como renglones ya
/// ajustados al ancho (32 o 48 columnas).
/// </summary>
public static class CreditNoteTicketBuilder
{
    public const string Title = "NOTA DE CRÉDITO";

    private const string DateFormat = "dd/MM/yyyy HH:mm";

    public static TicketDocument Build(
        BusinessProfileDto? profile,
        CreditNoteTicketData note,
        int columns,
        TicketOptions? options = null,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 16);
        options ??= new TicketOptions();

        var lines = new List<TicketLine>();
        TicketHeader.Add(lines, profile, columns);

        lines.Add(new TicketLine(Title, TicketAlignment.Center, Bold: true));
        if (options.IsReprint)
        {
            lines.Add(new TicketLine(TicketBuilder.ReprintLegend, TicketAlignment.Center, Bold: true));
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(note.IssuedAtUtc, DateTimeKind.Utc), timeZone ?? TimeZoneInfo.Local);
        lines.Add(new TicketLine($"Folio: {note.Folio}", Bold: true));
        lines.Add(new TicketLine(local.ToString(DateFormat, CultureInfo.InvariantCulture)));
        lines.Add(new TicketLine($"Venta de origen: {note.SaleFolio}"));
        lines.Add(new TicketLine(new string('-', columns)));
        lines.Add(new TicketLine(TextWrap.TwoColumns("SALDO", TicketBuilder.FormatMoney(note.BalanceCents), columns), Bold: true));
        lines.Add(new TicketLine(new string('-', columns)));
        lines.AddRange(TextWrap.Wrap("Presente este folio al pagar. Sin vencimiento. No es canjeable por efectivo.", columns)
            .Select(t => new TicketLine(t)));

        return new TicketDocument(lines, profile?.Logo, columns, note.Folio);
    }
}
