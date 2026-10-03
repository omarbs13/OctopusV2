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

    [Fact]
    public void ConDatosDelNegocio_ResumenEmpiezaConElEncabezadoCanonico()
    {
        var bytes = new XlsxReportWriter(new ChartRenderer()).Write(ReportWritersTestData.Document(2));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var summary = workbook.Worksheet("Resumen");
        Assert.Equal(
            ["Tienda Ñandú", "Calle Falsa 123", "Tel. 555-1234", "RFC: XAXX010101000", string.Empty, "Reporte de prueba"],
            Enumerable.Range(1, 6).Select(r => summary.Cell(r, 1).GetString()));
        Assert.True(summary.Cell(1, 1).Style.Font.Bold);
    }

    [Fact]
    public void SinDatosDelNegocio_ResumenMuestraElAvisoEnLaPrimeraFila()
    {
        var bytes = new XlsxReportWriter(new ChartRenderer()).Write(ReportWritersTestData.Document(2, withBusiness: false));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal("Datos del negocio no capturados", workbook.Worksheet("Resumen").Cell(1, 1).GetString());
    }
}
