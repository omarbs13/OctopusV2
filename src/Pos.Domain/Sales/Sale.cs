using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>
/// Venta registrada (agregado). Las ventas nunca se borran: se cancelan. <c>CreatedAt</c> es la
/// fecha de la venta y <c>CreatedBy</c> el usuario; los asigna la persistencia.
/// </summary>
public sealed class Sale
{
    public const int CancellationReasonMaxLength = 250;

    private readonly List<SaleLine> _lines = [];
    private readonly List<SalePayment> _payments = [];

    private Sale()
    {
    }

    public Guid Id { get; private set; }

    public long FolioNumber { get; private set; }

    /// <summary>Clave de idempotencia: un borrador solo produce una venta (research §4).</summary>
    public Guid DraftId { get; private set; }

    /// <summary>Turno de caja en que se hizo la venta. Nulo en las ventas anteriores a 0.6.0 (sin llave foránea, research §5).</summary>
    public Guid? CashShiftId { get; private set; }

    public long TotalCents { get; private set; }

    public SaleStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public Guid? CancelledBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<SaleLine> Lines => _lines;

    public IReadOnlyList<SalePayment> Payments => _payments;

    public Money Total => Money.FromCents(TotalCents);

    public string Folio => Sales.Folio.Format(FolioNumber);

    public static Sale Register(
        long folioNumber,
        Guid draftId,
        Guid? cashShiftId,
        IEnumerable<SaleLine> lines,
        IEnumerable<SalePayment> payments)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(payments);
        var lineList = lines.ToList();
        var paymentList = payments.ToList();

        if (folioNumber < 1)
        {
            throw new DomainException("El folio no es válido.");
        }

        if (cashShiftId == Guid.Empty)
        {
            throw new DomainException("La venta debe pertenecer a un turno.");
        }

        if (lineList.Count == 0)
        {
            throw new DomainException("La venta debe tener al menos una línea.");
        }

        var total = lineList.Sum(l => l.AmountCents);
        if (total is <= 0 or > Money.MaxCents)
        {
            throw new DomainException("El total de la venta debe ser mayor que 0 y no exceder el máximo permitido.");
        }

        if (paymentList.Sum(p => p.AmountCents) != total)
        {
            throw new DomainException("La suma de los pagos no coincide con el total de la venta.");
        }

        if (paymentList.Count(p => p.Method == PaymentMethod.Cash) > 1)
        {
            throw new DomainException("La venta admite a lo más un pago en efectivo.");
        }

        var sale = new Sale
        {
            Id = Guid.CreateVersion7(),
            FolioNumber = folioNumber,
            DraftId = draftId,
            CashShiftId = cashShiftId,
            TotalCents = total,
            Status = SaleStatus.Completed,
            Version = 1,
        };
        sale._lines.AddRange(lineList);
        sale._payments.AddRange(paymentList);
        return sale;
    }

    /// <summary>Cancela la venta completa (FR-033). Una venta cancelada no se puede volver a cancelar (FR-035).</summary>
    public void Cancel(string reason, DateTime utcNow, Guid userId)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new DomainException("Esta venta ya está cancelada");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text is null)
        {
            throw new DomainException("El motivo de la cancelación es obligatorio.");
        }

        if (text.Length > CancellationReasonMaxLength)
        {
            throw new DomainException($"El motivo admite hasta {CancellationReasonMaxLength} caracteres.");
        }

        Status = SaleStatus.Cancelled;
        CancellationReason = text;
        CancelledAt = utcNow;
        CancelledBy = userId;
    }

    /// <summary>Liga el movimiento <c>SALE_CANCEL</c> con la línea cuyo movimiento de salida regresa.</summary>
    public void LinkCancellationMovement(Guid lineId, Guid movementId)
    {
        if (Status != SaleStatus.Cancelled)
        {
            throw new DomainException("La venta debe estar cancelada.");
        }

        var line = _lines.Single(l => l.Id == lineId);
        line.LinkCancellationMovement(movementId);
    }
}
