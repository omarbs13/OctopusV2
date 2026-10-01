namespace Pos.Application.Reports;

public enum ChartKind
{
    Line,
    Bars,
    Pie,
}

/// <summary>Cómo se escriben los valores de una gráfica.</summary>
public enum ChartValueFormat
{
    /// <summary>Conteo (número entero).</summary>
    Count,

    /// <summary>Importe en centavos.</summary>
    Money,
}

/// <summary>Un punto de la gráfica; <c>Color</c> es un color hexadecimal <c>#RRGGBB</c> opcional.</summary>
public sealed record ChartPoint(string Label, long Value, string? Color = null);

/// <summary>Descripción neutral de una gráfica; las barras admiten valores negativos (research §8).</summary>
public sealed record ChartSpec(ChartKind Kind, string Title, IReadOnlyList<ChartPoint> Points, ChartValueFormat ValueFormat);
