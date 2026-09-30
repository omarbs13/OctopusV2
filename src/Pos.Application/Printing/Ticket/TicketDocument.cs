namespace Pos.Application.Printing.Ticket;

public enum TicketAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>Renglón ya ajustado al ancho del papel.</summary>
public sealed record TicketLine(string Text, TicketAlignment Alignment = TicketAlignment.Left, bool Bold = false);

/// <summary>Ticket listo para imprimir: renglones, logotipo opcional (imagen codificada), columnas (32 o 48) y folio.</summary>
public sealed record TicketDocument(IReadOnlyList<TicketLine> Lines, byte[]? Logo, int Columns, string Folio = "");

public sealed record TicketOptions(bool IsReprint = false);
