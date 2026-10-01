using ClosedXML.Excel;
using Pos.Infrastructure.Reports;

namespace Pos.Infrastructure.Tests.Reports;

public sealed class XlsxReportWriterTests
{
    [Fact]
    public void ElArchivoTieneLasTresHojasConImportesYFechasComoValores()
    {
        var bytes = new XlsxReportWriter(new ChartRenderer()).Write(ReportWritersTestData.Document(4));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Resumen", "Detalle", "Gráficas"], workbook.Worksheets.Select(w => w.Name));

        var detail = workbook.Worksheet("Detalle");

        // Fila 1 título, fila 2 encabezados, datos desde la 3.
        Assert.Equal("Folio", detail.Cell(2, 1).GetString());
        Assert.True(detail.Cell(3, 2).Value.IsDateTime);
        Assert.True(detail.Cell(3, 3).Value.IsNumber);
        Assert.Equal(1m, (decimal)detail.Cell(3, 3).Value.GetNumber());
        Assert.Equal(10m, detail.Rows(3, 6).Sum(r => (decimal)r.Cell(3).Value.GetNumber()));
        Assert.Equal(2, workbook.Worksheet("Gráficas").Pictures.Count);
    }
}
