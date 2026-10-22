namespace Pos.Domain.Sales;

/// <summary>Forma en que se vende una loseta: por pieza, por caja o por metro cuadrado (026).</summary>
public enum SaleMode
{
    Piece,
    Box,
    SquareMeter,
}

public static class SaleModes
{
    /// <summary>Unidad de medida de una modalidad.</summary>
    public static string UnitCode(this SaleMode mode) => mode switch
    {
        SaleMode.Box => "XBX",
        SaleMode.SquareMeter => "MTK",
        _ => "H87",
    };

    public static SaleMode? FromUnitCode(string unitCode) => unitCode switch
    {
        "XBX" => SaleMode.Box,
        "MTK" => SaleMode.SquareMeter,
        "H87" => SaleMode.Piece,
        _ => null,
    };
}
