using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>
/// Venta en curso, en memoria (research §1). Calcula importes y total; el ViewModel solo la
/// presenta. <see cref="DraftId"/> es la clave de idempotencia de la venta (research §4).
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

    public Money Total => TotalOf(_lines);

    public int ItemCount => _lines.Count;

    public bool CanCheckout => _lines.Count > 0 && Total.Cents > 0 && _lines.All(l => !l.IsUnavailable);

    /// <summary>Reconstruye la venta desde el borrador (FR-011).</summary>
    public static Cart Restore(Guid draftId, IEnumerable<CartLine> lines)
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
            cart._lines.Add(line);
        }

        // Falla si el total restaurado excede el máximo.
        _ = cart.Total;
        return cart;
    }

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

    public void Remove(Guid productId) => _lines.RemoveAll(l => l.ProductId == productId);

    /// <summary>Vacía la venta y comienza una nueva, con otro <see cref="DraftId"/>.</summary>
    public void Clear()
    {
        _lines.Clear();
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
            .ToList();

        // Valida todos los importes y el total antes de cambiar nada.
        _ = TotalOf(updated);
        _lines.Clear();
        _lines.AddRange(updated);
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
        _lines.Clear();
        _lines.AddRange(candidate);
    }
}
