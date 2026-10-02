namespace Pos.Application.Printing.PrintTicket;

/// <summary>Qué se imprime: una venta registrada o el ticket de prueba.</summary>
public abstract record PrintSource
{
    private protected PrintSource()
    {
    }

    public static PrintSource Sample { get; } = new SampleSource();

    public static PrintSource Sale(Guid saleId) => new SaleSource(saleId);

    /// <summary>Corte de un turno cerrado (008, FR-019).</summary>
    public static PrintSource ShiftReport(Guid shiftId) => new ShiftReportSource(shiftId);

    /// <summary>Comprobante de un movimiento de efectivo (008, FR-012).</summary>
    public static PrintSource CashMovement(Guid movementId) => new CashMovementSource(movementId);

    /// <summary>Ticket de una nota de crédito recién emitida o su reimpresión (013, FR-007a).</summary>
    public static PrintSource CreditNote(Guid creditNoteId) => new CreditNoteSource(creditNoteId);

    /// <summary>Recibo de un abono recién registrado o su reimpresión (014, FR-013).</summary>
    public static PrintSource CustomerPayment(Guid paymentId) => new CustomerPaymentSource(paymentId);

    /// <summary>Corte X o Z recién generado o su reimpresión desde el histórico (017, FR-014).</summary>
    public static PrintSource ShiftCut(Guid cutId) => new ShiftCutSource(cutId);

    public sealed record SaleSource(Guid SaleId) : PrintSource;

    public sealed record ShiftCutSource(Guid CutId) : PrintSource;

    public sealed record CustomerPaymentSource(Guid PaymentId) : PrintSource;

    public sealed record CreditNoteSource(Guid CreditNoteId) : PrintSource;

    public sealed record ShiftReportSource(Guid ShiftId) : PrintSource;

    public sealed record CashMovementSource(Guid MovementId) : PrintSource;

    public sealed record SampleSource : PrintSource;
}

public sealed record PrintTicketCommand(PrintSource Source, bool IsReprint = false);

/// <summary>Ticket impreso; <paramref name="Destination"/> es la impresora o la ruta del archivo.</summary>
public sealed record PrintedTicket(string Destination);
