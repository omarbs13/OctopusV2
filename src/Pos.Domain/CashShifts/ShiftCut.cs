using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

/// <summary>
/// Corte de caja (agregado inmutable, 017): un Corte X (lectura parcial del turno abierto) o un Corte
/// Z (cierre definitivo) con su folio consecutivo por tipo y una copia fija de las cifras del turno
/// (FR-006). No tiene métodos de modificación (FR-013). <c>CreatedAt</c>, <c>CreatedBy</c>,
/// <c>UpdatedAt</c> y <c>UpdatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class ShiftCut
{
    private ShiftCut()
    {
    }

    public Guid Id { get; private set; }

    public ShiftCutType Type { get; private set; }

    /// <summary>Consecutivo por tipo; se muestra como <c>X-000001</c> o <c>Z-000001</c>.</summary>
    public long Number { get; private set; }

    public Guid ShiftId { get; private set; }

    /// <summary>Número del turno al generar, para mostrar su folio sin unir tablas.</summary>
    public long ShiftNumber { get; private set; }

    public string RegisterCode { get; private set; } = CashRegister.Default;

    /// <summary>Dueño del turno.</summary>
    public Guid ShiftOpenedBy { get; private set; }

    public DateTime ShiftOpenedAt { get; private set; }

    public DateTime GeneratedAt { get; private set; }

    /// <summary>Quien generó el corte; en un Z, quien cerró el turno.</summary>
    public Guid GeneratedBy { get; private set; }

    /// <summary>Administrador que autorizó a un Cajero (solo X).</summary>
    public Guid? AuthorizedBy { get; private set; }

    public long OpeningFloatCents { get; private set; }

    public int SalesCount { get; private set; }

    public int CancelledCount { get; private set; }

    public long TotalSoldCents { get; private set; }

    public long CashSalesCents { get; private set; }

    public long CashCancelledCents { get; private set; }

    public long CardCents { get; private set; }

    public long TransferCents { get; private set; }

    public long CashRefundsCents { get; private set; }

    public long NonCashRefundsCents { get; private set; }

    public long CreditNotesIssuedCents { get; private set; }

    public long OnAccountSalesCents { get; private set; }

    public long CustomerPaymentsCashCents { get; private set; }

    public long CustomerPaymentsNonCashCents { get; private set; }

    public long CustomerPaymentVoidsCashCents { get; private set; }

    public long CustomerPaymentVoidsNonCashCents { get; private set; }

    public long DepositsCents { get; private set; }

    public long WithdrawalsCents { get; private set; }

    public long ExpectedCashCents { get; private set; }

    /// <summary>Solo Z; nulo en X (FR-004a).</summary>
    public long? CountedCashCents { get; private set; }

    /// <summary>Solo Z; contado − esperado, con signo.</summary>
    public long? DifferenceCents { get; private set; }

    /// <summary>Solo Z; comentario del arqueo.</summary>
    public string? Comment { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    /// <summary>Siempre nulo: los cortes no se borran. Existe por el Principio IV.</summary>
    public DateTime? DeletedAt { get; private set; }

    public int Version { get; private set; }

    public string Folio => ShiftCutFolio.Format(Type, Number);

    public string ShiftFolio => CashShifts.ShiftFolio.Format(ShiftNumber);

    public CashDifference? Difference => DifferenceCents is { } cents ? CashDifference.FromSigned(cents) : null;

    /// <summary>
    /// Corte X: copia las cifras del turno abierto en este momento. El esperado se calcula con la misma
    /// regla del cierre (<see cref="CashShift.ExpectedCash"/>). No modifica el turno (FR-003).
    /// </summary>
    public static ShiftCut Readout(long number, CashShift shift, ShiftSalesTotals totals, Guid generatedBy, Guid? authorizedBy, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(totals);

        if (shift.Status != CashShiftStatus.Open)
        {
            throw new DomainException("El turno ya está cerrado.");
        }

        var cut = Create(ShiftCutType.Readout, number, shift, generatedBy);
        cut.GeneratedAt = utcNow;
        cut.AuthorizedBy = authorizedBy;
        cut.SalesCount = totals.SalesCount;
        cut.CancelledCount = totals.CancelledCount;
        cut.TotalSoldCents = totals.TotalSoldCents;
        cut.CashSalesCents = totals.CashSalesCents;
        cut.CashCancelledCents = totals.CashCancelledCents;
        cut.CardCents = totals.CardCents;
        cut.TransferCents = totals.TransferCents;
        cut.CashRefundsCents = totals.CashRefundsCents;
        cut.NonCashRefundsCents = totals.NonCashRefundsCents;
        cut.CreditNotesIssuedCents = totals.CreditNotesIssuedCents;
        cut.OnAccountSalesCents = totals.OnAccountSalesCents;
        cut.CustomerPaymentsCashCents = totals.CustomerPaymentsCashCents;
        cut.CustomerPaymentsNonCashCents = totals.CustomerPaymentsNonCashCents;
        cut.CustomerPaymentVoidsCashCents = totals.CustomerPaymentVoidsCashCents;
        cut.CustomerPaymentVoidsNonCashCents = totals.CustomerPaymentVoidsNonCashCents;
        cut.DepositsCents = shift.DepositsTotalCents;
        cut.WithdrawalsCents = shift.WithdrawalsTotalCents;
        cut.ExpectedCashCents = shift.ExpectedCash(totals);
        return cut;
    }

    /// <summary>
    /// Corte Z: copia la instantánea que <see cref="CashShift.Close"/> acaba de guardar en el turno,
    /// así ambas fuentes son idénticas (research §2).
    /// </summary>
    public static ShiftCut Closing(long number, CashShift shift, Guid closedBy)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (shift.Status != CashShiftStatus.Closed || shift.ClosedAt is not { } closedAt || shift.CountedCashCents is not { } counted)
        {
            throw new DomainException("El Corte Z exige un turno cerrado con su arqueo.");
        }

        var cut = Create(ShiftCutType.Closing, number, shift, closedBy);
        cut.GeneratedAt = closedAt;
        cut.SalesCount = shift.SalesCount ?? 0;
        cut.CancelledCount = shift.CancelledCount ?? 0;
        cut.TotalSoldCents = shift.TotalSoldCents ?? 0;
        cut.CashSalesCents = shift.CashSalesCents ?? 0;
        cut.CashCancelledCents = shift.CashCancelledCents ?? 0;
        cut.CardCents = shift.CardCents ?? 0;
        cut.TransferCents = shift.TransferCents ?? 0;
        cut.CashRefundsCents = shift.CashRefundsCents ?? 0;
        cut.NonCashRefundsCents = shift.NonCashRefundsCents ?? 0;
        cut.CreditNotesIssuedCents = shift.CreditNotesIssuedCents ?? 0;
        cut.OnAccountSalesCents = shift.OnAccountSalesCents ?? 0;
        cut.CustomerPaymentsCashCents = shift.CustomerPaymentsCashCents ?? 0;
        cut.CustomerPaymentsNonCashCents = shift.CustomerPaymentsNonCashCents ?? 0;
        cut.CustomerPaymentVoidsCashCents = shift.CustomerPaymentVoidsCashCents ?? 0;
        cut.CustomerPaymentVoidsNonCashCents = shift.CustomerPaymentVoidsNonCashCents ?? 0;
        cut.DepositsCents = shift.DepositsCents ?? 0;
        cut.WithdrawalsCents = shift.WithdrawalsCents ?? 0;
        cut.ExpectedCashCents = shift.ExpectedCashCents ?? 0;
        cut.CountedCashCents = counted;
        cut.DifferenceCents = shift.DifferenceCents;
        cut.Comment = shift.ClosingComment;
        return cut;
    }

    private static ShiftCut Create(ShiftCutType type, long number, CashShift shift, Guid generatedBy)
    {
        if (number < 1)
        {
            throw new DomainException("El número de corte no es válido.");
        }

        if (generatedBy == Guid.Empty)
        {
            throw new DomainException("El corte debe tener un usuario.");
        }

        return new ShiftCut
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Number = number,
            ShiftId = shift.Id,
            ShiftNumber = shift.Number,
            RegisterCode = shift.RegisterCode,
            ShiftOpenedBy = shift.OpenedBy,
            ShiftOpenedAt = shift.OpenedAt,
            OpeningFloatCents = shift.OpeningFloatCents,
            GeneratedBy = generatedBy,
            Version = 1,
        };
    }
}
