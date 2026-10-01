using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>
/// Cancelación o devolución parcial de una venta (agregado inmutable, research §1). Se corrige con
/// otro registro, nunca se edita ni se borra. <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class SaleReturn
{
    public const int ReasonMaxLength = 250;

    private readonly List<SaleReturnLine> _lines = [];
    private readonly List<SaleReturnRefund> _refunds = [];

    private SaleReturn()
    {
        Reason = string.Empty;
    }

    public Guid Id { get; private set; }

    public long Number { get; private set; }

    public Guid SaleId { get; private set; }

    public ReturnKind Kind { get; private set; }

    public string Reason { get; private set; }

    public Guid AuthorizedBy { get; private set; }

    public long TotalCents { get; private set; }

    public ReturnCompensation Compensation { get; private set; }

    /// <summary>Turno abierto en que se hizo; nulo si el módulo Turnos no tiene licencia.</summary>
    public Guid? CashShiftId { get; private set; }

    public Guid? CreditNoteId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public IReadOnlyList<SaleReturnLine> Lines => _lines;

    public IReadOnlyList<SaleReturnRefund> Refunds => _refunds;

    public string Folio => ReturnFolio.Format(Number);

    public static SaleReturn Create(
        Guid id,
        long number,
        Guid saleId,
        ReturnKind kind,
        string reason,
        Guid authorizedBy,
        ReturnCompensation compensation,
        Guid? cashShiftId,
        IEnumerable<SaleReturnLine> lines,
        IEnumerable<SaleReturnRefund> refunds,
        Guid? creditNoteId)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(refunds);
        var lineList = lines.ToList();
        var refundList = refunds.ToList();

        if (id == Guid.Empty || number < 1 || saleId == Guid.Empty || authorizedBy == Guid.Empty)
        {
            throw new DomainException("La devolución no es válida.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text is null)
        {
            throw new DomainException("El motivo es obligatorio.");
        }

        if (text.Length > ReasonMaxLength)
        {
            throw new DomainException($"El motivo admite hasta {ReasonMaxLength} caracteres.");
        }

        if (lineList.Count == 0)
        {
            throw new DomainException("La devolución debe tener al menos una línea.");
        }

        var total = lineList.Sum(l => l.AmountCents);
        if (total <= 0)
        {
            throw new DomainException("El monto de la devolución debe ser mayor que 0.");
        }

        if (compensation == ReturnCompensation.Refund)
        {
            if (creditNoteId is not null || refundList.Sum(r => r.AmountCents) != total)
            {
                throw new DomainException("La suma de los reintegros no coincide con el total devuelto.");
            }
        }
        else if (refundList.Count > 0 || creditNoteId is null)
        {
            throw new DomainException("La devolución con nota de crédito no tiene reintegros y debe llevar la nota.");
        }

        var saleReturn = new SaleReturn
        {
            Id = id,
            Number = number,
            SaleId = saleId,
            Kind = kind,
            Reason = text,
            AuthorizedBy = authorizedBy,
            TotalCents = total,
            Compensation = compensation,
            CashShiftId = cashShiftId,
            CreditNoteId = creditNoteId,
        };
        saleReturn._lines.AddRange(lineList);
        saleReturn._refunds.AddRange(refundList);
        return saleReturn;
    }
}
