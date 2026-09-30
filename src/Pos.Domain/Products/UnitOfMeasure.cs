namespace Pos.Domain.Products;

/// <summary>
/// Unidad de medida del catálogo fijo (003, clarificación 1). Las claves son las del catálogo de
/// unidades del SAT; el catálogo no es editable y se siembra en la base con HasData.
/// </summary>
public sealed record UnitOfMeasure(string Code, string Name, int SortOrder)
{
    public const int CodeMaxLength = 3;
    public const int NameMaxLength = 50;

    public static UnitOfMeasure Piece { get; } = new("H87", "Pieza", 1);

    public static IReadOnlyList<UnitOfMeasure> All { get; } =
    [
        Piece,
        new("KGM", "Kilogramo", 2),
        new("GRM", "Gramo", 3),
        new("LTR", "Litro", 4),
        new("MLT", "Mililitro", 5),
        new("MTR", "Metro", 6),
        new("XBX", "Caja", 7),
        new("XPK", "Paquete", 8),
    ];

    /// <summary>Valor inicial de un producto nuevo y de los productos existentes al actualizar.</summary>
    public static UnitOfMeasure Default => Piece;

    /// <summary>Indica si la clave existe en el catálogo (comparación ordinal exacta).</summary>
    public static bool IsValidCode(string? code) =>
        code is not null && All.Any(u => string.Equals(u.Code, code, StringComparison.Ordinal));
}
