using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>
/// Existencia de un producto, en milésimas enteras y con signo: solo las ventas pueden dejarla bajo
/// cero (research §3). <see cref="Quantity"/> sigue sin admitir negativos porque es lo que se captura.
/// </summary>
public readonly record struct StockLevel : IComparable<StockLevel>
{
    private StockLevel(long thousandths) => Thousandths = thousandths;

    public long Thousandths { get; }

    public static StockLevel Zero => default;

    public bool IsNegative => Thousandths < 0;

    public static StockLevel FromThousandths(long thousandths)
    {
        if (thousandths is < -Quantity.MaxStockThousandths or > Quantity.MaxStockThousandths)
        {
            throw new DomainException(
                $"La existencia debe estar entre -{Quantity.MaxStockThousandths} y {Quantity.MaxStockThousandths} milésimas.");
        }

        return new StockLevel(thousandths);
    }

    public static StockLevel From(Quantity quantity) => FromThousandths(quantity.Thousandths);

    /// <summary>Indica si la existencia no tiene más decimales que los permitidos.</summary>
    public bool FitsDecimals(int decimalPlaces)
    {
        var places = Math.Clamp(decimalPlaces, 0, 3);
        var step = (long)Math.Pow(10, 3 - places);
        return Thousandths % step == 0;
    }

    public int CompareTo(StockLevel other) => Thousandths.CompareTo(other.Thousandths);

    public static StockLevel operator +(StockLevel left, Quantity right) =>
        FromThousandths(checked(left.Thousandths + right.Thousandths));

    public static StockLevel operator -(StockLevel left, Quantity right) =>
        FromThousandths(checked(left.Thousandths - right.Thousandths));

    public static bool operator <(StockLevel left, StockLevel right) => left.Thousandths < right.Thousandths;

    public static bool operator >(StockLevel left, StockLevel right) => left.Thousandths > right.Thousandths;

    public static bool operator <=(StockLevel left, StockLevel right) => left.Thousandths <= right.Thousandths;

    public static bool operator >=(StockLevel left, StockLevel right) => left.Thousandths >= right.Thousandths;

    public static bool operator <(StockLevel left, Quantity right) => left.Thousandths < right.Thousandths;

    public static bool operator >(StockLevel left, Quantity right) => left.Thousandths > right.Thousandths;

    public static bool operator <=(StockLevel left, Quantity right) => left.Thousandths <= right.Thousandths;

    public static bool operator >=(StockLevel left, Quantity right) => left.Thousandths >= right.Thousandths;

    public static bool operator <(Quantity left, StockLevel right) => left.Thousandths < right.Thousandths;

    public static bool operator >(Quantity left, StockLevel right) => left.Thousandths > right.Thousandths;

    public static bool operator <=(Quantity left, StockLevel right) => left.Thousandths <= right.Thousandths;

    public static bool operator >=(Quantity left, StockLevel right) => left.Thousandths >= right.Thousandths;
}
