using Pos.Domain.Common;
using Pos.Domain.Suppliers;

namespace Pos.Domain.Purchases;

/// <summary>
/// Compra: una factura de proveedor registrada como entrada de mercancía (agregado inmutable, 020,
/// research §1). La única transición es <c>ACTIVE → VOIDED</c> (<see cref="Void"/>); no hay métodos de
/// edición (FR-017). <c>CreatedAt/By</c> y <c>UpdatedAt/By</c> los asigna la persistencia.
/// </summary>
public sealed class Purchase
{
    public const int InvoiceNumberMaxLength = 50;
    public const int VoidReasonMaxLength = 250;

    private readonly List<PurchaseLine> _lines = [];

    private Purchase()
    {
        SupplierName = string.Empty;
        InvoiceNumber = string.Empty;
        InvoiceKey = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Nombre del proveedor al registrar (FR-016).</summary>
    public string SupplierName { get; private set; }

    /// <summary>Número de factura recortado, tal como se capturó.</summary>
    public string InvoiceNumber { get; private set; }

    /// <summary>Número de factura en mayúsculas invariantes, para detectar duplicados (research §5).</summary>
    public string InvoiceKey { get; private set; }

    public DateOnly InvoiceDate { get; private set; }

    public int LineCount { get; private set; }

    public long SubtotalCents { get; private set; }

    public long TaxCents { get; private set; }

    public long TotalCents { get; private set; }

    public PurchaseStatus Status { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedBy { get; private set; }

    public string? VoidReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<PurchaseLine> Lines => _lines;

    /// <summary>Recorta y pasa a mayúsculas invariantes; es la clave del duplicado por proveedor.</summary>
    public static string NormalizeInvoice(string? invoiceNumber) =>
        (invoiceNumber ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Registra la compra con sus líneas en el orden de captura: al menos una, una por producto, fecha
    /// no futura, impuestos ≥ 0, subtotal > 0 (FR-008a) y todos los importes ≤ <see cref="Money.MaxCents"/>.
    /// </summary>
    public static Purchase Register(
        Guid supplierId,
        string supplierName,
        string invoiceNumber,
        DateOnly invoiceDate,
        DateOnly today,
        IEnumerable<PurchaseLineDraft> lines,
        long taxCents)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var drafts = lines.ToList();

        var name = (supplierName ?? string.Empty).Trim();
        if (supplierId == Guid.Empty || name.Length is 0 or > Supplier.NameMaxLength)
        {
            throw new DomainException("El proveedor de la compra no es válido.");
        }

        var invoice = (invoiceNumber ?? string.Empty).Trim();
        if (invoice.Length is 0 or > InvoiceNumberMaxLength)
        {
            throw new DomainException($"El número de factura es obligatorio y admite hasta {InvoiceNumberMaxLength} caracteres.");
        }

        if (invoiceDate > today)
        {
            throw new DomainException("La fecha de la factura no puede ser futura.");
        }

        if (drafts.Count == 0)
        {
            throw new DomainException("La compra debe tener al menos una línea.");
        }

        if (drafts.Select(d => d.ProductId).Distinct().Count() != drafts.Count)
        {
            throw new DomainException("Cada producto solo puede aparecer en una línea de la compra.");
        }

        if (taxCents < 0)
        {
            throw new DomainException("Los impuestos deben ser mayores o iguales que 0.");
        }

        if (drafts.Any(d => PurchaseMath.LineExceedsMaximum(d.QuantityThousandths, d.UnitCostCents)))
        {
            throw new DomainException("El importe de una línea excede el máximo permitido.");
        }

        var purchase = new Purchase
        {
            Id = Guid.CreateVersion7(),
            SupplierId = supplierId,
            SupplierName = name,
            InvoiceNumber = invoice,
            InvoiceKey = NormalizeInvoice(invoice),
            InvoiceDate = invoiceDate,
            Status = PurchaseStatus.Active,
            Version = 1,
        };

        for (var i = 0; i < drafts.Count; i++)
        {
            purchase._lines.Add(PurchaseLine.Create(purchase.Id, i + 1, drafts[i]));
        }

        var amounts = purchase._lines.Select(l => l.AmountCents).ToList();
        var exceeded = PurchaseMath.ExceededLimit(amounts, taxCents);
        if (exceeded != PurchaseAmountLimit.None)
        {
            throw new DomainException(exceeded switch
            {
                PurchaseAmountLimit.Subtotal => "El subtotal de la compra excede el máximo permitido.",
                PurchaseAmountLimit.Tax => "Los impuestos exceden el máximo permitido.",
                PurchaseAmountLimit.Total => "El total de la compra excede el máximo permitido.",
                _ => "El importe de una línea excede el máximo permitido.",
            });
        }

        var subtotal = PurchaseMath.Subtotal(amounts);
        if (subtotal <= 0)
        {
            throw new DomainException("El subtotal de la compra debe ser mayor que $0.00.");
        }

        purchase.LineCount = drafts.Count;
        purchase.SubtotalCents = subtotal;
        purchase.TaxCents = taxCents;
        purchase.TotalCents = PurchaseMath.Total(subtotal, taxCents);
        return purchase;
    }

    /// <summary>Enlaza el movimiento <c>PURCHASE</c> generado para la línea del producto.</summary>
    public void LinkMovement(Guid productId, Guid movementId) => LineOf(productId).LinkMovement(movementId);

    /// <summary>Enlaza el movimiento <c>PURCH_VOID</c> de la anulación; solo mientras la compra está vigente.</summary>
    public void LinkVoidMovement(Guid productId, Guid movementId)
    {
        if (Status != PurchaseStatus.Active)
        {
            throw new DomainException("La compra ya está anulada.");
        }

        LineOf(productId).LinkVoidMovement(movementId);
    }

    /// <summary>Única transición: anula la compra una sola vez, con motivo obligatorio (FR-017a, FR-017b).</summary>
    public void Void(string reason, Guid userId, DateTime utcNow)
    {
        if (Status != PurchaseStatus.Active)
        {
            throw new DomainException("La compra ya está anulada.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text is null or { Length: > VoidReasonMaxLength })
        {
            throw new DomainException($"El motivo es obligatorio y admite hasta {VoidReasonMaxLength} caracteres.");
        }

        Status = PurchaseStatus.Voided;
        VoidReason = text;
        VoidedBy = userId;
        VoidedAt = utcNow;
    }

    private PurchaseLine LineOf(Guid productId) =>
        _lines.SingleOrDefault(l => l.ProductId == productId)
        ?? throw new DomainException("El producto no forma parte de la compra.");
}
