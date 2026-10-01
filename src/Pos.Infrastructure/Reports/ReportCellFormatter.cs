using System.Globalization;
using Pos.Application.Reports.Export;

namespace Pos.Infrastructure.Reports;

/// <summary>Texto de cada celda para el PDF: importes en pesos, fechas en hora local, cantidades con los decimales de su unidad.</summary>
internal static class ReportCellFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static string Format(ReportCell cell) => cell switch
    {
        TextCell text => text.Text,
        CountCell count => count.Value.ToString("N0", Culture),
        MoneyCell money => (money.Cents / 100m).ToString("C2", Culture),
        DateCell date => LocalText(date.Utc),
        QuantityCell quantity => (quantity.Thousandths / 1000m).ToString("N" + Math.Clamp(quantity.DecimalPlaces, 0, 3), Culture),
        PercentCell percent => string.Create(Culture, $"{(percent.BasisPoints > 0 ? "+" : string.Empty)}{percent.BasisPoints / 100m:0.00} %"),
        _ => string.Empty,
    };

    public static string LocalText(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static bool IsNumeric(ReportColumnType type) => type is not (ReportColumnType.Text or ReportColumnType.Date);
}
