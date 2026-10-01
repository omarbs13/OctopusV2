using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Domain.Sales;

/// <summary>
/// Línea de una venta registrada, con copia del nombre, SKU, unidad y precio del momento (FR-023).
/// Es parte del agregado <see cref="Sale"/>.
/// </summary>
public sealed class SaleLine
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;

    private SaleLine()
    {
        ProductName = string.Empty;
        ProductSku = string.Empty;
        UnitCode = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public int Position { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public string ProductSku { get; private set; }

    public string UnitCode { get; private set; }

    public int DecimalPlaces { get; private set; }

    public long UnitPriceCents { get; private set; }

    public long QuantityThousandths { get; private set; }

    /// <summary>
    /// Importe neto, lo efectivamente pagado: original − descuento de línea − parte repartida del descuento
    /// de venta (015, research §5). Las devoluciones (013) y los reportes lo usan sin cambios.
    /// </summary>
    public long AmountCents { get; private set; }

    /// <summary>Cantidad × precio. En las ventas anteriores a 0.10.0 es igual a <see cref="AmountCents"/>.</summary>
    public long OriginalAmountCents { get; private set; }

    /// <summary>Descuento propio de la línea (015); 0 si no tiene.</summary>
    public long LineDiscountCents { get; private set; }

    /// <summary>Parte repartida del descuento global o del cupón (015, FR-004); 0 si no hay.</summary>
    public long OrderDiscountCents { get; private set; }

    /// <summary>Importe final de la línea que se muestra en el ticket y en la consulta: original − descuento de línea.</summary>
    public long AmountAfterLineDiscountCents => OriginalAmountCents - LineDiscountCents;

    /// <summary>Movimiento <c>SALE</c>; nulo si el producto no controlaba inventario.</summary>
    public Guid? SaleMovementId { get; private set; }

    /// <summary>Movimiento <c>SALE_CANCEL</c>, asignado al cancelar la venta.</summary>
    public Guid? CancellationMovementId { get; private set; }

    /// <summary>Milésimas devueltas acumuladas por devoluciones parciales (013).</summary>
    public long ReturnedQuantity { get; private set; }

    /// <summary>Milésimas que aún se pueden devolver.</summary>
    public long AvailableToReturn => QuantityThousandths - ReturnedQuantity;

    public Money UnitPrice => Money.FromCents(UnitPriceCents);

    public Quantity Quantity => Quantity.FromThousandths(QuantityThousandths);

    public Money Amount => Money.FromCents(AmountCents);

    public static SaleLine Create(
        int position,
        Guid productId,
        string productName,
        string productSku,
        string unitCode,
        int decimalPlaces,
        Money unitPrice,
        Quantity quantity,
        Guid? saleMovementId,
        long lineDiscountCents = 0,
        long orderDiscountCents = 0)
    {
        if (position < 1 || quantity <= Quantity.Zero)
        {
            throw new DomainException("La línea de venta no es válida.");
        }

        var original = SaleMath.LineAmount(quantity, unitPrice).Cents;
        if (lineDiscountCents < 0 || orderDiscountCents < 0 || lineDiscountCents + orderDiscountCents > original)
        {
            throw new DomainException("El descuento de la línea no puede ser mayor que su importe.");
        }

        if (string.IsNullOrWhiteSpace(productName) || productName.Length > NameMaxLength
            || string.IsNullOrWhiteSpace(productSku) || productSku.Length > SkuMaxLength
            || string.IsNullOrWhiteSpace(unitCode) || unitCode.Length > UnitOfMeasure.CodeMaxLength)
        {
            throw new DomainException("Los datos del producto de la línea no son válidos.");
        }

        return new SaleLine
        {
            Id = Guid.CreateVersion7(),
            Position = position,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            UnitCode = unitCode,
            DecimalPlaces = decimalPlaces,
            UnitPriceCents = unitPrice.Cents,
            QuantityThousandths = quantity.Thousandths,
            OriginalAmountCents = original,
            LineDiscountCents = lineDiscountCents,
            OrderDiscountCents = orderDiscountCents,
            AmountCents = original - lineDiscountCents - orderDiscountCents,
            SaleMovementId = saleMovementId,
        };
    }

    /// <summary>Solo lo llama <see cref="Sale.ApplyReturn"/>.</summary>
    internal void AddReturned(long thousandths) => ReturnedQuantity += thousandths;

    /// <summary>Solo lo llama <see cref="Sale.Cancel"/>.</summary>
    internal void LinkCancellationMovement(Guid movementId) => CancellationMovementId = movementId;
}
