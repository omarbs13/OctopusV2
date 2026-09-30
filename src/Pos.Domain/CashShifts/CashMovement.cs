namespace Pos.Domain.CashShifts;

/// <summary>
/// Ingreso o retiro de efectivo, inmutable (como <c>InventoryMovement</c>). Solo lo crea
/// <see cref="CashShift"/>. <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class CashMovement
{
    public const int ReasonMaxLength = 250;

    private CashMovement()
    {
    }

    internal CashMovement(
        Guid cashShiftId,
        int sequence,
        CashMovementType type,
        long amountCents,
        string reason,
        Guid? authorizedBy)
    {
        Id = Guid.CreateVersion7();
        CashShiftId = cashShiftId;
        Sequence = sequence;
        Type = type;
        AmountCents = amountCents;
        Reason = reason;
        AuthorizedBy = authorizedBy;
    }

    public Guid Id { get; private set; }

    public Guid CashShiftId { get; private set; }

    /// <summary>1, 2, 3… dentro del turno; forma el folio <c>T-000123-02</c>.</summary>
    public int Sequence { get; private set; }

    public CashMovementType Type { get; private set; }

    public long AmountCents { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Administrador que autorizó el retiro de un cajero (FR-011).</summary>
    public Guid? AuthorizedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }
}
