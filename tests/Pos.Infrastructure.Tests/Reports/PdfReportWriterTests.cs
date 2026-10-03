using System.Text;
using System.Text.RegularExpressions;
using Pos.Infrastructure.Reports;
using SkiaSharp;

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

    [Fact]
    public void VariasPaginas_ElEncabezadoDelNegocioSoloEnLaPrimera()
    {
        // El logo es la única imagen del documento: solo la página que dibuja el encabezado la usa.
        var bytes = new PdfReportWriter().Write(ReportWritersTestData.Document(300, logo: Logo()));
        var text = Encoding.Latin1.GetString(bytes);

        Assert.True(Pages(bytes) >= 6);
        Assert.Equal(1, ImageResourceRegex().Count(text));
    }

    [Fact]
    public void LogoIlegible_SeOmiteYElPdfSeGenera()
    {
        var bytes = new PdfReportWriter().Write(ReportWritersTestData.Document(3, logo: [1, 2, 3]));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(0, ImageResourceRegex().Count(Encoding.Latin1.GetString(bytes)));
    }

    private static byte[] Logo()
    {
        using var bitmap = new SKBitmap(200, 80);
        bitmap.Erase(SKColors.SteelBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static int Pages(byte[] pdf) => PageRegex().Count(Encoding.Latin1.GetString(pdf));

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageRegex();

    [GeneratedRegex(@"/XObject\s*<<")]
    private static partial Regex ImageResourceRegex();
}
