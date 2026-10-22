using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Sales;

/// <summary>
/// Venta en curso, en memoria (research §1). Calcula importes, descuentos y total; el ViewModel solo la
/// presenta. <see cref="DraftId"/> es la clave de idempotencia de la venta (research §4). Orden del cálculo
/// (015, research §2): importe de línea → descuento de línea → subtotal → descuento de venta → total.
/// </summary>
public sealed class Cart
{
    private readonly List<CartLine> _lines = [];

    public Cart()
        : this(Guid.CreateVersion7())
    {
    }

    private Cart(Guid draftId) => DraftId = draftId;

    public Guid DraftId { get; private set; }

    public IReadOnlyList<CartLine> Lines => _lines;

    /// <summary>Descuento de la venta completa (manual o cupón); nulo si no hay.</summary>
    public OrderDiscount? OrderDiscount { get; private set; }

    /// <summary>
    /// Descuento global de monto fijo que el último cambio retiró porque superó el subtotal (Historia 2,
    /// escenario 5); nulo si el último cambio no retiró nada. La interfaz avisa al cajero.
    /// </summary>
    public OrderDiscount? LastRemovedOrderDiscount { get; private set; }

    /// <summary>Σ de los importes originales (cantidad × precio).</summary>
    public Money OriginalTotal => TotalOf(_lines);

    /// <summary>Σ de los importes después del descuento de cada línea.</summary>
    public Money Subtotal => Money.FromCents(_lines.Sum(l => l.NetBeforeOrder.Cents));

    /// <summary>Monto del descuento de venta sobre el subtotal; un cupón de monto se limita al subtotal (Historia 3, escenario 6).</summary>
    public Money OrderDiscountAmount => Money.FromCents(OrderDiscountOn(Subtotal.Cents, OrderDiscount));

    /// <summary>Total a cobrar, nunca negativo (FR-003).</summary>
    public Money Total => Money.FromCents(Subtotal.Cents - OrderDiscountAmount.Cents);

    /// <summary>Σ de todos los descuentos: "Usted ahorró".</summary>
    public Money TotalDiscount => Money.FromCents(OriginalTotal.Cents - Total.Cents);

    public bool HasDiscounts => OrderDiscount is not null || _lines.Any(l => l.HasDiscount);

    public int ItemCount => _lines.Count;

    /// <summary>
    /// Hay líneas, todas se pueden vender y el total es mayor que 0; el total puede ser 0 solo por descuentos
    /// (un descuento del 100 % autorizado, 015 research §6).
    /// </summary>
    public bool CanCheckout => _lines.Count > 0
        && _lines.All(l => !l.IsUnavailable)
        && (Total.Cents > 0 || TotalDiscount.Cents > 0);

    /// <summary>
    /// Reconstruye la venta desde el borrador (FR-011) con sus descuentos (015). Un descuento que ya no cabe
    /// en el importe vigente se descarta.
    /// </summary>
    public static Cart Restore(Guid draftId, IEnumerable<CartLine> lines, OrderDiscount? orderDiscount = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var cart = new Cart(draftId);
        foreach (var line in lines)
        {
            if (cart._lines.Any(l => l.ProductId == line.ProductId))
            {
                continue;
            }

            ValidateQuantity(line.Quantity, line.DecimalPlaces);
            cart._lines.Add(line.DiscountFits() ? line : line with { Discount = null });
        }

        // Falla si el total restaurado excede el máximo.
        _ = cart.OriginalTotal;
        cart.OrderDiscount = orderDiscount;
        cart.RecalculateOrderDiscount();
        return cart;
    }

    /// <summary>
    /// Pone o quita (<c>null</c>) el descuento de una línea (FR-001, FR-007). Valida que no exceda el importe
    /// ni redondee a $0.00; si falla, la línea no cambia. Un solo descuento por línea: el nuevo reemplaza al anterior.
    /// </summary>
    public void SetLineDiscount(Guid productId, LineDiscount? discount)
    {
        var index = _lines.FindIndex(l => l.ProductId == productId);
        if (index < 0)
        {
            throw new DomainException("El producto no está en la venta.");
        }

        Replace(index, _lines[index] with { Discount = discount });
    }

    /// <summary>
    /// Pone, reemplaza o quita (<c>null</c>) el descuento de la venta (FR-002, FR-013). La confirmación del
    /// reemplazo entre cupón y descuento manual la pide la interfaz. Un descuento manual de monto mayor que
    /// el subtotal, o cualquiera que redondee a $0.00, se rechaza y la venta no cambia.
    /// </summary>
    public void SetOrderDiscount(OrderDiscount? discount)
    {
        LastRemovedOrderDiscount = null;
        var subtotal = Subtotal.Cents;
        switch (discount)
        {
            case OrderDiscount.Manual manual:
                _ = DiscountMath.Amount(subtotal, manual.Value);
                break;
            case OrderDiscount.CouponApplied coupon when subtotal > 0 && OrderDiscountOn(subtotal, coupon) == 0:
                throw new DomainException("El descuento resulta en $0.00.");
        }

        OrderDiscount = discount;
    }

    /// <summary>
    /// Reparte el descuento de venta entre las líneas en proporción a su importe después del descuento de
    /// línea, por resto mayor y con empate por orden de captura (FR-004). La suma es exactamente el descuento.
    /// </summary>
    public IReadOnlyList<long> Allocation() =>
        Proportional.Allocate(OrderDiscountAmount.Cents, [.. _lines.Select(l => l.NetBeforeOrder.Cents)]);

    /// <summary>Agrega el producto; si ya está, incrementa su línea (FR-003). Rechaza no vendibles (FR-007).</summary>
    public void Add(CartProduct product, Quantity? quantity = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.UnavailableReason is { } reason)
        {
            throw new DomainException(NotSellableMessage(product.Name, reason));
        }

        var added = quantity ?? Quantity.FromThousandths(1000);
        var index = _lines.FindIndex(l => l.ProductId == product.ProductId);
        if (index < 0)
        {
            ValidateQuantity(added, product.DecimalPlaces);
            var line = new CartLine(
                product.ProductId,
                product.Name,
                product.Sku,
                product.UnitCode,
                product.DecimalPlaces,
                product.TracksInventory,
                product.UnitPrice,
                added);
            Replace(-1, line);
            return;
        }

        var current = _lines[index];
        var sum = checked(current.Quantity.Thousandths + added.Thousandths);
        ValidateQuantity(QuantityFrom(sum), current.DecimalPlaces);
        Replace(index, current with { Quantity = QuantityFrom(sum) });
    }

    /// <summary>Cambia la cantidad de una línea; si falla, conserva el valor anterior.</summary>
    public void SetQuantity(Guid productId, Quantity quantity)
    {
        var index = _lines.FindIndex(l => l.ProductId == productId);
        if (index < 0)
        {
            throw new DomainException("El producto no está en la venta.");
        }

        var current = _lines[index];
        ValidateQuantity(quantity, current.DecimalPlaces);
        Replace(index, current with { Quantity = quantity });
    }

    public void Remove(Guid productId)
    {
        _lines.RemoveAll(l => l.ProductId == productId);
        RecalculateOrderDiscount();
    }

    /// <summary>Vacía la venta y comienza una nueva, con otro <see cref="DraftId"/> y sin descuentos.</summary>
    public void Clear()
    {
        _lines.Clear();
        OrderDiscount = null;
        LastRemovedOrderDiscount = null;
        DraftId = Guid.CreateVersion7();
    }

    /// <summary>Reemplaza los precios con los vigentes y marca las líneas que ya no se pueden vender.</summary>
    public void ApplyCurrentPrices(IEnumerable<CartPriceUpdate> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        var byProduct = prices.ToDictionary(p => p.ProductId);
        var updated = _lines
            .Select(l => byProduct.TryGetValue(l.ProductId, out var p)
                ? l with { UnitPrice = p.UnitPrice, UnavailableReason = p.UnavailableReason }
                : l)
            .Select(l => l.DiscountFits() ? l : l with { Discount = null })
            .ToList();

        // Valida todos los importes y el total antes de cambiar nada.
        _ = TotalOf(updated);
        _lines.Clear();
        _lines.AddRange(updated);
        RecalculateOrderDiscount();
    }

    public static string NotSellableMessage(string name, UnavailableReason reason) => reason == UnavailableReason.Deleted
        ? $"{name} fue eliminado y no se puede vender"
        : $"{name} está inactivo y no se puede vender";

    private static Quantity QuantityFrom(long thousandths)
    {
        if (thousandths > Quantity.MaxCaptureThousandths)
        {
            throw new DomainException("La cantidad excede el máximo permitido.");
        }

        return Quantity.FromThousandths(thousandths);
    }

    private static void ValidateQuantity(Quantity quantity, int decimalPlaces)
    {
        if (quantity <= Quantity.Zero)
        {
            throw new DomainException("La cantidad debe ser mayor que 0.");
        }

        if (quantity.Thousandths > Quantity.MaxCaptureThousandths)
        {
            throw new DomainException("La cantidad excede el máximo permitido.");
        }

        if (!quantity.FitsDecimals(decimalPlaces))
        {
            throw new DomainException(decimalPlaces == 0
                ? "La cantidad debe ser un número entero."
                : $"La cantidad admite hasta {decimalPlaces} decimales.");
        }
    }

    /// <summary>
    /// Monto del descuento de venta sobre <paramref name="subtotal"/>. Porcentaje: mitad hacia arriba. Monto
    /// manual: el valor (la venta lo retira si excede). Cupón de monto: limitado al subtotal.
    /// </summary>
    private static long OrderDiscountOn(long subtotal, OrderDiscount? discount)
    {
        if (discount is null || subtotal <= 0)
        {
            return 0;
        }

        var value = discount.Value;
        var amount = value.Mode == DiscountMode.Percent
            ? ((subtotal * value.Raw) + 5_000) / DiscountValue.MaxBasisPoints
            : value.Raw;
        return Math.Min(amount, subtotal);
    }

    /// <summary>Un descuento manual de monto que ya no cabe en el subtotal se retira y se informa (Historia 2, escenario 5).</summary>
    private void RecalculateOrderDiscount()
    {
        LastRemovedOrderDiscount = null;
        if (OrderDiscount is OrderDiscount.Manual { Value.Mode: DiscountMode.Amount } manual && manual.Value.Raw > Subtotal.Cents)
        {
            LastRemovedOrderDiscount = manual;
            OrderDiscount = null;
        }
    }

    private static Money TotalOf(IEnumerable<CartLine> lines)
    {
        long total = 0;
        foreach (var line in lines)
        {
            total = checked(total + line.Amount.Cents);
            if (total > Money.MaxCents)
            {
                throw new DomainException("El total de la venta excede el máximo permitido.");
            }
        }

        return Money.FromCents(total);
    }

    /// <summary>Reemplaza (o agrega, con índice negativo) una línea validando importe y total.</summary>
    private void Replace(int index, CartLine line)
    {
        var candidate = new List<CartLine>(_lines);
        if (index < 0)
        {
            candidate.Add(line);
        }
        else
        {
            candidate[index] = line;
        }

        _ = TotalOf(candidate);

        // 015: un monto fijo que excede el nuevo importe, o un porcentaje que redondea a $0.00, rechaza el
        // cambio y conserva la línea anterior (spec, casos límite).
        _ = line.LineDiscountAmount;
        _lines.Clear();
        _lines.AddRange(candidate);
        RecalculateOrderDiscount();
    }
}
