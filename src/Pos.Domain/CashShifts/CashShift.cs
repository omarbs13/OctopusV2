using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

/// <summary>
/// Turno de caja (agregado): periodo de trabajo de un usuario en la caja, desde la apertura hasta el
/// cierre con arqueo. Al cerrar guarda una instantánea inmutable de los totales (research §10).
/// Los turnos nunca se borran. <c>CreatedAt</c>, <c>CreatedBy</c>, <c>UpdatedAt</c> y
/// <c>UpdatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class CashShift
{
    public const int RegisterCodeMaxLength = 20;
    public const int CommentMaxLength = 250;

    private readonly List<CashMovement> _movements = [];

    private CashShift()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Consecutivo único; se muestra como <c>T-000123</c>.</summary>
    public long Number { get; private set; }

    public string RegisterCode { get; private set; } = CashRegister.Default;

    public CashShiftStatus Status { get; private set; }

    /// <summary>Dueño del turno: el único que vende en él.</summary>
    public Guid OpenedBy { get; private set; }

    public DateTime OpenedAt { get; private set; }

    public long OpeningFloatCents { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    /// <summary>Quien cerró: el dueño o un administrador (FR-024).</summary>
    public Guid? ClosedBy { get; private set; }

    public int? SalesCount { get; private set; }

    public int? CancelledCount { get; private set; }

    public long? TotalSoldCents { get; private set; }

    public long? CashSalesCents { get; private set; }

    public long? CashCancelledCents { get; private set; }

    public long? CardCents { get; private set; }

    public long? TransferCents { get; private set; }

    /// <summary>Efectivo devuelto en el turno (013); nulo en turnos cerrados antes de 0.8.0.</summary>
    public long? CashRefundsCents { get; private set; }

    public long? NonCashRefundsCents { get; private set; }

    public long? CreditNotesIssuedCents { get; private set; }

    public long? DepositsCents { get; private set; }

    public long? WithdrawalsCents { get; private set; }

    public long? ExpectedCashCents { get; private set; }

    public long? CountedCashCents { get; private set; }

    /// <summary>Contado − esperado, con signo (research §4).</summary>
    public long? DifferenceCents { get; private set; }

    public string? ClosingComment { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    /// <summary>Siempre nulo: los turnos no se borran. Existe por el Principio IV.</summary>
    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<CashMovement> Movements => _movements;

    public string Folio => ShiftFolio.Format(Number);

    public Money OpeningFloat => Money.FromCents(OpeningFloatCents);

    /// <summary>Suma de ingresos del turno (en vivo mientras está abierto).</summary>
    public long DepositsTotalCents => _movements.Where(m => m.Type == CashMovementType.In).Sum(m => m.AmountCents);

    /// <summary>Suma de retiros del turno (en vivo mientras está abierto).</summary>
    public long WithdrawalsTotalCents => _movements.Where(m => m.Type == CashMovementType.Out).Sum(m => m.AmountCents);

    public CashDifference? Difference => DifferenceCents is { } cents ? CashDifference.FromSigned(cents) : null;

    public static CashShift Open(long number, Money openingFloat, Guid openedBy, DateTime utcNow)
    {
        if (number < 1)
        {
            throw new DomainException("El número de turno no es válido.");
        }

        if (openedBy == Guid.Empty)
        {
            throw new DomainException("El turno debe tener un usuario.");
        }

        return new CashShift
        {
            Id = Guid.CreateVersion7(),
            Number = number,
            RegisterCode = CashRegister.Default,
            Status = CashShiftStatus.Open,
            OpenedBy = openedBy,
            OpenedAt = utcNow,
            OpeningFloatCents = openingFloat.Cents,
            Version = 1,
        };
    }

    /// <summary>Efectivo esperado con los movimientos actuales del turno y los totales de sus ventas (FR-015).</summary>
    public long ExpectedCash(ShiftSalesTotals totals) =>
        CashShiftMath.ExpectedCash(OpeningFloatCents, totals, DepositsTotalCents, WithdrawalsTotalCents);

    public CashMovement RecordDeposit(Money amount, string reason) =>
        Record(CashMovementType.In, amount, reason, authorizedBy: null);

    /// <summary>
    /// Registra un retiro. <paramref name="expectedCashCents"/> es el efectivo esperado en ese momento;
    /// un retiro mayor se rechaza (FR-010).
    /// </summary>
    public CashMovement RecordWithdrawal(Money amount, string reason, long expectedCashCents, Guid? authorizedBy)
    {
        if (amount.Cents > expectedCashCents)
        {
            throw new InsufficientCashException(expectedCashCents);
        }

        return Record(CashMovementType.Out, amount, reason, authorizedBy);
    }

    /// <summary>
    /// Cierra el turno con el arqueo: calcula el esperado con <see cref="CashShiftMath"/>, exige un
    /// comentario si hay diferencia (FR-017) y guarda la instantánea.
    /// </summary>
    public void Close(ShiftSalesTotals totals, Money counted, string? comment, Guid closedBy, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(totals);
        EnsureOpen();

        var deposits = DepositsTotalCents;
        var withdrawals = WithdrawalsTotalCents;
        var expected = CashShiftMath.ExpectedCash(OpeningFloatCents, totals, deposits, withdrawals);
        var difference = counted.Cents - expected;

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (difference != 0 && text is null)
        {
            throw new DomainException("El comentario es obligatorio cuando hay diferencia.");
        }

        if (text is { Length: > CommentMaxLength })
        {
            throw new DomainException($"El comentario admite hasta {CommentMaxLength} caracteres.");
        }

        SalesCount = totals.SalesCount;
        CancelledCount = totals.CancelledCount;
        TotalSoldCents = totals.TotalSoldCents;
        CashSalesCents = totals.CashSalesCents;
        CashCancelledCents = totals.CashCancelledCents;
        CardCents = totals.CardCents;
        TransferCents = totals.TransferCents;
        CashRefundsCents = totals.CashRefundsCents;
        NonCashRefundsCents = totals.NonCashRefundsCents;
        CreditNotesIssuedCents = totals.CreditNotesIssuedCents;
        DepositsCents = deposits;
        WithdrawalsCents = withdrawals;
        ExpectedCashCents = expected;
        CountedCashCents = counted.Cents;
        DifferenceCents = difference;
        ClosingComment = text;
        ClosedAt = utcNow;
        ClosedBy = closedBy;
        Status = CashShiftStatus.Closed;
    }

    private CashMovement Record(CashMovementType type, Money amount, string reason, Guid? authorizedBy)
    {
        EnsureOpen();

        if (amount.Cents <= 0)
        {
            throw new DomainException("El monto debe ser mayor que 0.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text is null)
        {
            throw new DomainException("El motivo es obligatorio.");
        }

        if (text.Length > CashMovement.ReasonMaxLength)
        {
            throw new DomainException($"El motivo admite hasta {CashMovement.ReasonMaxLength} caracteres.");
        }

        var movement = new CashMovement(Id, _movements.Count + 1, type, amount.Cents, text, authorizedBy);
        _movements.Add(movement);
        return movement;
    }

    private void EnsureOpen()
    {
        if (Status != CashShiftStatus.Open)
        {
            throw new DomainException("El turno ya está cerrado.");
        }
    }
}
