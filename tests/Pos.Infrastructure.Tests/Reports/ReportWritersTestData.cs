using Pos.Application.Reports;
using Pos.Application.Reports.Export;

namespace Pos.Infrastructure.Tests.Reports;

internal static class ReportWritersTestData
{
    public static ReportDocument Document(int rows, bool withBusiness = true) =>
        new(
            "Reporte de prueba",
            "01/09/2026 - 30/09/2026",
            ["Cajero: Ana", "Orden: Fecha (descendente)"],
            [new ReportMetric("Total vendido", new MoneyCell(123_450)), new ReportMetric("Ventas", new CountCell(rows))],
            [
                new ReportTable(
                    "Detalle",
                    [
                        new ReportColumn("Folio", ReportColumnType.Text),
                        new ReportColumn("Fecha", ReportColumnType.Date),
                        new ReportColumn("Total", ReportColumnType.Money),
                        new ReportColumn("Diferencia %", ReportColumnType.Percent),
                    ],
                    [.. Enumerable.Range(1, rows).Select(i => (IReadOnlyList<ReportCell>)
                    [
                        new TextCell($"V-{i:000000} Jalapeño"),
                        new DateCell(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)),
                        new MoneyCell(i * 100),
                        i % 2 == 0 ? new PercentCell(-600) : EmptyCell.Instance,
                    ])]),
            ],
            [
                new ChartSpec(ChartKind.Bars, "Diferencia", [new ChartPoint("T-1", -6_000), new ChartPoint("T-2", 2_000)], ChartValueFormat.Money),
                new ChartSpec(ChartKind.Pie, "Estados", [new ChartPoint("Normal", 3, "#2E7D32"), new ChartPoint("Baja", 1, "#EF6C00")], ChartValueFormat.Count),
            ],
            withBusiness ? new ReportBusiness("Tienda Ñandú", "Calle Falsa 123", "555-1234") : null,
            new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc),
            "Administrador");
}
