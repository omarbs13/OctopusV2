using System.Text;
using System.Text.RegularExpressions;
using Pos.Infrastructure.Reports;

namespace Pos.Infrastructure.Tests.Reports;

public sealed partial class PdfReportWriterTests
{
    [Fact]
    public void ElPdfEmpiezaConLaFirmaYUnaTablaLargaOcupaVariasPaginas()
    {
        var small = new PdfReportWriter().Write(ReportWritersTestData.Document(5));
        var large = new PdfReportWriter().Write(ReportWritersTestData.Document(300));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(large, 0, 4));
        Assert.True(Pages(large) > Pages(small));
        Assert.True(Pages(large) >= 6);
    }

    [Fact]
    public void SinDatosDelNegocio_NoFalla()
    {
        var bytes = new PdfReportWriter().Write(ReportWritersTestData.Document(3, withBusiness: false));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static int Pages(byte[] pdf) => PageRegex().Count(Encoding.Latin1.GetString(pdf));

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageRegex();
}
