namespace Pos.Application.Returns;

/// <summary>Mensajes de devoluciones y notas de crédito, en español.</summary>
public static class ReturnMessages
{
    public const string ReasonRequired = "Capture el motivo.";
    public const string ReasonTooLong = "El motivo admite hasta 250 caracteres.";
    public const string LinesRequired = "Seleccione al menos un artículo y no exceda la cantidad disponible";
    public const string WindowRange = "El plazo debe estar entre 1 y 3650 días.";
    public const string FolioRequired = "Capture el folio de la nota de crédito.";
    public const string FolioInvalid = "Capture un folio válido, por ejemplo NC-000123 o 123.";
    public const string CreditNoteNotFound = "La nota de crédito no existe o no tiene saldo";
    public const string AlreadyReversed = "El reintegro ya no está pendiente de reversa";
    public const string PartiallyReturned = "La venta tiene devoluciones parciales; devuelva el resto con una devolución parcial";
    public const string CreditNoteOnlyOne = "La venta admite a lo más un pago con nota de crédito.";

    public static string WindowExpired(int days) =>
        $"La venta tiene más de {days} días y ya no admite devoluciones";

    public static string InsufficientCreditNote(string available) => $"El saldo de la nota es {available}";
}
