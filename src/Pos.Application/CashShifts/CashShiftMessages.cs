namespace Pos.Application.CashShifts;

/// <summary>Mensajes de turnos de caja, en español.</summary>
public static class CashShiftMessages
{
    public const string OpeningFloatInvalid = "Capture un fondo inicial válido.";
    public const string OpeningFloatZeroNeedsConfirmation = "Confirme que desea abrir el turno sin fondo inicial.";
    public const string AmountInvalid = "Capture un monto mayor que 0.";
    public const string AmountTooLarge = "El monto excede el máximo permitido.";
    public const string ReasonRequired = "Capture el motivo del movimiento.";
    public const string ReasonTooLong = "El motivo admite hasta 250 caracteres.";
    public const string CountedInvalid = "Capture un efectivo contado válido.";
    public const string CommentRequired = "Capture un comentario: el efectivo contado no coincide con el esperado.";
    public const string CommentTooLong = "El comentario admite hasta 250 caracteres.";
    public const string DateRangeInvalid = "La fecha inicial no puede ser posterior a la final.";

    public const string ShiftRequired = "Abra un turno para vender";
    public const string ShiftAlreadyOpen = "Ya hay un turno abierto";
    public const string ShiftClosed = "El turno ya está cerrado";
    public const string SaleInProgress = "Termine o cancele la venta en curso antes de cerrar el turno";
    public const string ShiftChanged = "El turno cambió. Revise las cifras de nuevo";
    public const string WithdrawalExceedsCash = "El retiro excede el efectivo disponible en caja";
    public const string RefundExceedsCash = "No hay efectivo suficiente en caja para devolver esta venta. Registre un ingreso e intente de nuevo";
    public const string SaleFromClosedShift = "La venta pertenece a un turno cerrado";

    public static string ShiftOwnedByOther(string name) =>
        $"Hay un turno abierto de {name}. Debe cerrarse antes de vender";

    public static string HeldSaleWillBeDiscarded(string name) =>
        $"{name} tiene una venta en curso guardada. Si continúa, se descartará";

    public static string WithdrawalExceedsCashWithAvailable(string available) =>
        $"{WithdrawalExceedsCash}. Disponible: {available}";
}
