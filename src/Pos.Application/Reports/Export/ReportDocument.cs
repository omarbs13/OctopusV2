using Pos.Application.Business;

namespace Pos.Application.Reports.Export;

/// <summary>Celda de una tabla exportada; cada tipo conserva su valor numérico o de fecha para el XLSX (FR-019).</summary>
public abstract record ReportCell;

public sealed record TextCell(string Text) : ReportCell;

public sealed record CountCell(long Value) : ReportCell;

/// <summary>Importe en centavos.</summary>
public sealed record MoneyCell(long Cents) : ReportCell;

/// <summary>Fecha y hora en UTC; los escritores la convierten a hora local.</summary>
public sealed record DateCell(DateTime Utc) : ReportCell;

/// <summary>Cantidad de inventario en milésimas con los decimales de su unidad.</summary>
public sealed record QuantityCell(long Thousandths, int DecimalPlaces) : ReportCell;

/// <summary>Porcentaje en centésimas de por ciento (500 = 5 %).</summary>
public sealed record PercentCell(long BasisPoints) : ReportCell;

/// <summary>Celda sin valor (por ejemplo, el efectivo contado de un turno en curso).</summary>
public sealed record EmptyCell : ReportCell
{
    public static EmptyCell Instance { get; } = new();
}

public enum ReportColumnType
{
    Text,
    Count,
    Money,
    Date,
    Quantity,
    Percent,
}

public sealed record ReportColumn(string Header, ReportColumnType Type);

public sealed record ReportTable(string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<ReportCell>> Rows);

public sealed record ReportMetric(string Label, ReportCell Value);

/// <summary>
/// Documento neutral que los escritores de PDF y XLSX convierten a archivo. Se arma en Application, así
/// que la omisión de datos de turnos abiertos se aplica aquí y no en los escritores (research §11).
/// </summary>
public sealed record ReportDocument(
    string Title,
    string PeriodText,
    IReadOnlyList<string> FilterTexts,
    IReadOnlyList<ReportMetric> Metrics,
    IReadOnlyList<ReportTable> Tables,
    IReadOnlyList<ChartSpec> Charts,
    BusinessHeader? Business,
    DateTime GeneratedAtUtc,
    string GeneratedBy);
