using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>
/// Cuenta por cobrar de una venta a crédito (agregado, 014, research §3). Guarda el saldo junto a su
/// libro inmutable de movimientos con la invariante <c>BalanceCents = OriginalCents + Σ entradas</c>,
/// siempre entre 0 y <see cref="OriginalCents"/>. Nunca se borra: se cancela. El repositorio la carga
/// con todas sus entradas. <c>CreatedAt</c> (fecha de la venta y origen del plazo) y el resto de los
/// campos de auditoría los asigna la persistencia.
/// </summary>
public sealed class Receivable
{
    public const int CustomerNameMaxLength = 120;

    private readonly List<ReceivableEntry> _entries = [];

    private Receivable()
    {
        CustomerName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Nombre del cliente al vender; es el que imprime el ticket (research §2).</summary>
    public string CustomerName { get; private set; }

    public long OriginalCents { get; private set; }

    public long BalanceCents { get; private set; }

    public ReceivableStatus Status { get; private set; }

    /// <summary>Administrador que autorizó vender sobre el límite (FR-007); nulo si no hubo excedente.</summary>
    public Guid? OverLimitAuthorizedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<ReceivableEntry> Entries => _entries;

    public static Receivable Create(Guid saleId, Guid customerId, string customerName, long originalCents, Guid? overLimitAuthorizedBy)
    {
        var name = (customerName ?? string.Empty).Trim();
        if (saleId == Guid.Empty || customerId == Guid.Empty || name.Length == 0)
        {
            throw new DomainException("La cuenta por cobrar no es válida.");
        }

        if (originalCents is <= 0 or > Money.MaxCents)
        {
            throw new DomainException("El monto de la cuenta por cobrar debe ser mayor que 0.");
        }

        return new Receivable
        {
            Id = Guid.CreateVersion7(),
            SaleId = saleId,
            CustomerId = customerId,
            CustomerName = name.Length <= CustomerNameMaxLength ? name : name[..CustomerNameMaxLength],
            OriginalCents = originalCents,
            BalanceCents = originalCents,
            Status = ReceivableStatus.Pending,
            OverLimitAuthorizedBy = overLimitAuthorizedBy,
            Version = 1,
        };
    }

    /// <summary>Aplica parte de un abono (<c>PAYMENT</c>); no puede exceder el saldo (FR-011).</summary>
    public void ApplyPayment(Guid paymentId, long amountCents)
    {
        EnsurePending();
        EnsureWithinBalance(amountCents);
        Append(ReceivableEntryType.Payment, -amountCents, paymentId, null);
    }

    /// <summary>Revierte exactamente lo que el abono aplicó a esta cuenta (<c>PAYMENT_VOID</c>, FR-014).</summary>
    public void RevertPayment(Guid paymentId)
    {
        EnsureNotCancelled();
        var applied = -_entries.Where(e => e.Type == ReceivableEntryType.Payment && e.CustomerPaymentId == paymentId).Sum(e => e.AmountCents);
        var reverted = _entries.Where(e => e.Type == ReceivableEntryType.PaymentVoid && e.CustomerPaymentId == paymentId).Sum(e => e.AmountCents);
        var pending = applied - reverted;
        if (pending <= 0)
        {
            throw new DomainException("El abono no tiene nada aplicado a esta cuenta.");
        }

        Append(ReceivableEntryType.PaymentVoid, pending, paymentId, null);
    }

    /// <summary>
    /// Registra lo devuelto o cancelado de la venta (<c>RETURN</c>). Si el cliente ya había abonado
    /// sobre esa parte, libera el excedente (<c>EXCESS_OUT</c>) para aplicarlo a otras cuentas o
    /// reintegrarlo (research §8).
    /// </summary>
    public void ApplyReturn(Guid saleReturnId, long amountCents, out long excessCents)
    {
        EnsureNotCancelled();
        if (amountCents <= 0)
        {
            throw new DomainException("El monto devuelto debe ser mayor que 0.");
        }

        excessCents = Math.Max(0, amountCents - BalanceCents);
        _entries.Add(new ReceivableEntry(Id, ReceivableEntryType.Return, -amountCents, null, saleReturnId));
        if (excessCents > 0)
        {
            _entries.Add(new ReceivableEntry(Id, ReceivableEntryType.ExcessOut, excessCents, null, saleReturnId));
        }

        Recalculate();
    }

    /// <summary>Aplica el excedente liberado por la devolución de otra venta (<c>EXCESS_IN</c>).</summary>
    public void ApplyExcess(Guid saleReturnId, long amountCents)
    {
        EnsurePending();
        EnsureWithinBalance(amountCents);
        Append(ReceivableEntryType.ExcessIn, -amountCents, null, saleReturnId);
    }

    /// <summary>La venta se canceló completa o quedó totalmente devuelta: la cuenta termina sin saldo.</summary>
    public void Cancel()
    {
        EnsureNotCancelled();
        if (BalanceCents != 0)
        {
            throw new DomainException("La cuenta por cobrar tiene saldo; no se puede cancelar.");
        }

        Status = ReceivableStatus.Cancelled;
    }

    private void Append(ReceivableEntryType type, long amountCents, Guid? paymentId, Guid? saleReturnId)
    {
        _entries.Add(new ReceivableEntry(Id, type, amountCents, paymentId, saleReturnId));
        Recalculate();
    }

    private void Recalculate()
    {
        var balance = OriginalCents + _entries.Sum(e => e.AmountCents);
        if (balance < 0 || balance > OriginalCents)
        {
            throw new DomainException("El saldo de la cuenta por cobrar quedaría fuera de rango.");
        }

        BalanceCents = balance;
        if (Status != ReceivableStatus.Cancelled)
        {
            Status = balance == 0 ? ReceivableStatus.Paid : ReceivableStatus.Pending;
        }
    }

    private void EnsurePending()
    {
        if (Status != ReceivableStatus.Pending)
        {
            throw new DomainException("La cuenta por cobrar no está pendiente.");
        }
    }

    private void EnsureNotCancelled()
    {
        if (Status == ReceivableStatus.Cancelled)
        {
            throw new DomainException("La cuenta por cobrar está cancelada.");
        }
    }

    private void EnsureWithinBalance(long amountCents)
    {
        if (amountCents <= 0 || amountCents > BalanceCents)
        {
            throw new DomainException("El monto debe ser mayor que 0 y no exceder el saldo de la cuenta.");
        }
    }
}
