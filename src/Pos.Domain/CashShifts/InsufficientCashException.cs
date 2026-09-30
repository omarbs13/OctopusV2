namespace Pos.Domain.CashShifts;

/// <summary>
/// Un retiro excede el efectivo esperado. Application decide si el mensaje muestra el monto
/// disponible (FR-010).
/// </summary>
public sealed class InsufficientCashException : Exception
{
    public InsufficientCashException(long availableCents)
        : base("El retiro excede el efectivo disponible en caja.") => AvailableCents = availableCents;

    public InsufficientCashException()
        : this(0)
    {
    }

    public InsufficientCashException(string message)
        : base(message)
    {
    }

    public InsufficientCashException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public long AvailableCents { get; }
}
