namespace Pos.Application.Printing.PrintTicket;

/// <summary>Qué se imprime: una venta registrada o el ticket de prueba.</summary>
public abstract record PrintSource
{
    private protected PrintSource()
    {
    }

    public static PrintSource Sample { get; } = new SampleSource();

    public static PrintSource Sale(Guid saleId) => new SaleSource(saleId);

    public sealed record SaleSource(Guid SaleId) : PrintSource;

    public sealed record SampleSource : PrintSource;
}

public sealed record PrintTicketCommand(PrintSource Source, bool IsReprint = false);

/// <summary>Ticket impreso; <paramref name="Destination"/> es la impresora o la ruta del archivo.</summary>
public sealed record PrintedTicket(string Destination);
