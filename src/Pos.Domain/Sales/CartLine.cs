using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Motivo por el que una línea de la venta en curso ya no se puede vender.</summary>
public enum UnavailableReason
{
    Inactive,
    Deleted,
}

/// <summary>Producto que se agrega a la venta en curso, con los datos que la venta necesita.</summary>
public sealed record CartProduct(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitCode,
    int DecimalPlaces,
    bool TracksInventory,
    Money UnitPrice,
    UnavailableReason? UnavailableReason = null);

/// <summary>Precio y disponibilidad vigentes de un producto (revisión antes de cobrar).</summary>
public sealed record CartPriceUpdate(Guid ProductId, Money UnitPrice, UnavailableReason? UnavailableReason);

/// <summary>Línea de la venta en curso. El importe siempre se deduce de cantidad y precio.</summary>
public sealed record CartLine(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitCode,
    int DecimalPlaces,
    bool TracksInventory,
    Money UnitPrice,
    Quantity Quantity,
    UnavailableReason? UnavailableReason = null)
{
    public Money Amount => SaleMath.LineAmount(Quantity, UnitPrice);

    public bool IsUnavailable => UnavailableReason is not null;
}
