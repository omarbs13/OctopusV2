using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// XLSX editable con ClosedXML (research §7): hoja "Resumen" (título, período, filtros y métricas),
/// "Detalle" (tablas con importes y fechas como valores, sin fórmulas ni protección) y "Gráficas"
/// (imágenes PNG del mismo renderizador que la pantalla).
/// </summary>
public sealed class XlsxReportWriter : IXlsxReportWriter
{
    private const string MoneyFormat = "#,##0.00";
    private const string DateFormat = "dd/mm/yyyy hh:mm";
    private const string PercentFormat = "0.00%";
    private const int ChartWidth = 720;
    private const int ChartHeight = 300;
    private const int ChartRows = 17;

    private readonly IChartRenderer _charts;

    public XlsxReportWriter(IChartRenderer charts) => _charts = charts;

    public byte[] Write(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var workbook = new XLWorkbook();
        WriteSummary(workbook.AddWorksheet("Resumen"), document);
        WriteDetail(workbook.AddWorksheet("Detalle"), document);
        WriteCharts(workbook.AddWorksheet("Gráficas"), document);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(IXLWorksheet sheet, ReportDocument document)
    {
        var row = 1;
        if (document.Business is { } business)
        {
            sheet.Cell(row, 1).Value = business.Name;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
            sheet.Cell(row++, 1).Value = $"{business.Address} · Tel. {business.Phone}";
        }

        sheet.Cell(row, 1).Value = document.Title;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row++;
        sheet.Cell(row++, 1).Value = document.PeriodText;
        foreach (var filter in document.FilterTexts)
        {
            sheet.Cell(row++, 1).Value = filter;
        }

        row++;
        foreach (var metric in document.Metrics)
        {
            sheet.Cell(row, 1).Value = metric.Label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            WriteCell(sheet.Cell(row, 2), metric.Value);
            row++;
        }

        row++;
        sheet.Cell(row, 1).Value = $"Generado el {ReportCellFormatter.LocalText(document.GeneratedAtUtc)} por {document.GeneratedBy}";
        sheet.Columns(1, 2).AdjustToContents(1, row, 12, 70);
    }

    private static void WriteDetail(IXLWorksheet sheet, ReportDocument document)
    {
        var row = 1;
        var columns = 1;
        foreach (var table in document.Tables)
        {
            sheet.Cell(row, 1).Value = table.Title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;

            for (var c = 0; c < table.Columns.Count; c++)
            {
                var header = sheet.Cell(row, c + 1);
                header.Value = table.Columns[c].Header;
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            row++;
            foreach (var cells in table.Rows)
            {
                for (var c = 0; c < cells.Count; c++)
                {
                    WriteCell(sheet.Cell(row, c + 1), cells[c]);
                }

                row++;
            }

            columns = Math.Max(columns, table.Columns.Count);
            row++;
        }

        sheet.Columns(1, columns).AdjustToContents(1, row, 10, 60);
    }

    private void WriteCharts(IXLWorksheet sheet, ReportDocument document)
    {
        if (document.Charts.Count == 0)
        {
            sheet.Cell(1, 1).Value = "Este reporte no tiene gráficas.";
            return;
        }

        var row = 1;
        foreach (var chart in document.Charts)
        {
            using var png = new MemoryStream(_charts.RenderPng(chart, ChartWidth, ChartHeight));
            sheet.AddPicture(png, XLPictureFormat.Png).MoveTo(sheet.Cell(row, 1));
            row += ChartRows;
        }
    }

    private static void WriteCell(IXLCell cell, ReportCell value)
    {
        switch (value)
        {
            case TextCell text:
                cell.Value = text.Text;
                break;
            case CountCell count:
                cell.Value = count.Value;
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case MoneyCell money:
                cell.Value = money.Cents / 100m;
                cell.Style.NumberFormat.Format = MoneyFormat;
                break;
            case DateCell date:
                cell.Value = DateTime.SpecifyKind(date.Utc, DateTimeKind.Utc).ToLocalTime();
                cell.Style.NumberFormat.Format = DateFormat;
                break;
            case QuantityCell quantity:
                cell.Value = quantity.Thousandths / 1000m;
                cell.Style.NumberFormat.Format = quantity.DecimalPlaces > 0
                    ? "#,##0." + new string('0', Math.Clamp(quantity.DecimalPlaces, 0, 3))
                    : "#,##0";
                break;
            case PercentCell percent:
                cell.Value = percent.BasisPoints / 10_000m;
                cell.Style.NumberFormat.Format = PercentFormat;
                break;
            default:
                break;
        }
    }
}
