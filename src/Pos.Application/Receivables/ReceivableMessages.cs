namespace Pos.Application.Receivables;

/// <summary>Mensajes de abonos y crédito, en español.</summary>
public static class ReceivableMessages
{
    public const string MethodInvalid = "La forma de pago del abono debe ser efectivo, tarjeta o transferencia.";
    public const string ReferenceTooLong = "La referencia admite hasta 50 caracteres.";
    public const string ReasonRequired = "Capture el motivo.";
    public const string ReasonTooLong = "El motivo admite hasta 250 caracteres.";
    public const string RequestRequired = "Vuelva a abrir el formulario del abono.";
    public const string AlreadyVoided = "El abono ya está anulado";
    public const string ReturnedAfter = "Este abono se aplicó a una venta que después se canceló o devolvió; no se puede anular";
    public const string TermRange = "El plazo debe estar entre 1 y 3650 días.";
    public const string CashRefundNeedsReturns = "Cancelar esta venta a crédito devolvería efectivo al cliente por lo que abonó de más; para registrar ese reintegro se requiere el módulo Devoluciones.";
    public const string CreditNoteNotAllowed = "Las ventas a crédito no admiten nota de crédito; la devolución reduce el saldo del cliente.";
}
