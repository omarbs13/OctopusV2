using Pos.Application.Business;
using Pos.Application.CashShifts;
using Pos.Application.CreditNotes;
using Pos.Application.Printing.Ticket;
using Pos.Application.Receivables;
using Pos.Application.Sales;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;

namespace Pos.Application.Tests.Printing;

/// <summary>Armado del ticket: ajuste de texto, alineación, leyendas, pagos y cambio (006, SC-003).</summary>
public sealed class TicketBuilderTests
{
    private static readonly DateTime Created = new(2026, 9, 30, 14, 5, 0, DateTimeKind.Utc);

    private static readonly BusinessProfileDto Profile =
        new("Mi Tienda", "Calle 1 #23, Col. Centro", "555 123 4567", "XAXX010101000", "Gracias por su compra", null);

    private static SaleLineDto Line(int position, string name, long thousandths, int decimals, long priceCents, long amountCents) =>
        new(position, Guid.NewGuid(), name, "SKU", decimals == 0 ? "H87" : "KGM", decimals, priceCents, thousandths, amountCents);

    private static SaleDetailDto Sale(
        IReadOnlyList<SaleLineDto> lines,
        IReadOnlyList<SalePaymentDto> payments,
        long total,
        SaleStatus status = SaleStatus.Completed) =>
        new(Guid.NewGuid(), "V-000123", Created, "Ana", total, status, 1, null, null, null, lines, payments, Guid.NewGuid());

    private static SaleDetailDto Simple() =>
        Sale(
            [Line(1, "Refresco cola 600 ml", 2000, 0, 1800, 3600)],
            [new SalePaymentDto(PaymentMethod.Cash, 3600, 5000, 1400, null)],
            3600);

    private static List<string> Text(TicketDocument document) => [.. document.Lines.Select(l => l.Text)];

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void NingunRenglonSuperaLasColumnas(int columns)
    {
        var sale = Sale(
            [
                Line(1, "Queso oaxaca de rancho tradicional extra largo con muchísimas palabras y más", 1250, 3, 15000, 18750),
                Line(2, new string('X', 100), 1000, 0, 99999, 99999),
            ],
            [new SalePaymentDto(PaymentMethod.Card, 118749, null, null, null)],
            118749);

        var document = TicketBuilder.Build(Profile, sale, columns, timeZone: TimeZoneInfo.Utc);

        Assert.All(document.Lines, l => Assert.True(l.Text.Length <= columns, $"'{l.Text}' mide {l.Text.Length}"));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void DescripcionLarga_ContinuaConSangriaYElImporteVaALaDerechaEnLaUltimaLinea(int columns)
    {
        var sale = Sale(
            [Line(1, "Queso oaxaca de rancho tradicional extra", 1250, 3, 15000, 18750)],
            [new SalePaymentDto(PaymentMethod.Card, 18750, null, null, null)],
            18750);

        var lines = Text(TicketBuilder.Build(null, sale, columns, timeZone: TimeZoneInfo.Utc));

        var first = lines.FindIndex(l => l.StartsWith("1.250 Queso", StringComparison.Ordinal));
        Assert.True(first >= 0);
        var last = lines.FindIndex(first, l => l.EndsWith("$187.50", StringComparison.Ordinal));
        Assert.True(last > first, "el importe debe ir en una línea posterior a la primera");
        Assert.All(lines.Skip(first + 1).Take(last - first), l => Assert.StartsWith("      ", l, StringComparison.Ordinal));
        Assert.Equal(columns, lines[last].Length);
        Assert.DoesNotContain(lines.Take(last).Skip(first), l => l.Contains("$187.50", StringComparison.Ordinal));
    }

    [Fact]
    public void DescripcionCorta_LlevaElImporteEnLaMismaLinea()
    {
        var lines = Text(TicketBuilder.Build(null, Simple(), 32, timeZone: TimeZoneInfo.Utc));

        var row = Assert.Single(lines, l => l.StartsWith("2 Refresco", StringComparison.Ordinal));
        Assert.EndsWith("$36.00", row, StringComparison.Ordinal);
        Assert.Equal(32, row.Length);
    }

    [Fact]
    public void PalabraMasLargaQueElEspacio_SePartePeroNoPierdeCaracteres()
    {
        var name = new string('A', 40) + "B";
        var sale = Sale(
            [Line(1, name, 1000, 0, 100, 100)],
            [new SalePaymentDto(PaymentMethod.Transfer, 100, null, null, null)],
            100);

        var lines = Text(TicketBuilder.Build(null, sale, 32, timeZone: TimeZoneInfo.Utc));

        var separators = lines.Select((l, i) => (l, i)).Where(x => x.l.StartsWith("----", StringComparison.Ordinal)).Select(x => x.i).ToList();
        var body = string.Concat(lines
            .Skip(separators[0] + 1)
            .Take(separators[1] - separators[0] - 1)
            .Select(l => l.Replace("$1.00", string.Empty, StringComparison.Ordinal).Trim()));
        body = body.Replace(" ", string.Empty, StringComparison.Ordinal)[1..];
        Assert.Equal(name, body);
    }

    [Fact]
    public void Cantidad_UsaLosDecimalesDeLaUnidad()
    {
        var sale = Sale(
            [Line(1, "Pieza", 3000, 0, 100, 300), Line(2, "Queso", 1250, 3, 10000, 12500)],
            [new SalePaymentDto(PaymentMethod.Card, 12800, null, null, null)],
            12800);

        var lines = Text(TicketBuilder.Build(null, sale, 48, timeZone: TimeZoneInfo.Utc));

        Assert.Contains(lines, l => l.StartsWith("3 Pieza", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("1.250 Queso", StringComparison.Ordinal));
    }

    [Fact]
    public void PagosMixtos_MuestranCadaFormaYElCambioSoloSiElEfectivoLoGenero()
    {
        var sale = Sale(
            [Line(1, "Producto", 1000, 0, 30000, 30000)],
            [
                new SalePaymentDto(PaymentMethod.Card, 10000, null, null, "1234"),
                new SalePaymentDto(PaymentMethod.Cash, 20000, 25000, 5000, null),
            ],
            30000);

        var lines = Text(TicketBuilder.Build(Profile, sale, 32, timeZone: TimeZoneInfo.Utc));

        Assert.Contains(lines, l => l.StartsWith("TOTAL", StringComparison.Ordinal) && l.EndsWith("$300.00", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Tarjeta", StringComparison.Ordinal) && l.EndsWith("$100.00", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Efectivo recibido", StringComparison.Ordinal) && l.EndsWith("$250.00", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("CAMBIO", StringComparison.Ordinal) && l.EndsWith("$50.00", StringComparison.Ordinal));
    }

    [Fact]
    public void EfectivoExacto_NoMuestraCambio()
    {
        var sale = Sale(
            [Line(1, "Producto", 1000, 0, 5000, 5000)],
            [new SalePaymentDto(PaymentMethod.Cash, 5000, 5000, 0, null)],
            5000);

        var lines = Text(TicketBuilder.Build(null, sale, 32, timeZone: TimeZoneInfo.Utc));

        Assert.DoesNotContain(lines, l => l.StartsWith("CAMBIO", StringComparison.Ordinal));
    }

    [Fact]
    public void Leyendas_CanceladaSeDeduceDelEstado_ReimpresionSoloConLaOpcion()
    {
        var normal = Text(TicketBuilder.Build(Profile, Simple(), 32, timeZone: TimeZoneInfo.Utc));
        Assert.DoesNotContain(TicketBuilder.CancelledLegend, normal);
        Assert.DoesNotContain(TicketBuilder.ReprintLegend, normal);

        var cancelled = Simple() with { Status = SaleStatus.Cancelled };
        var cancelledLines = Text(TicketBuilder.Build(Profile, cancelled, 32, timeZone: TimeZoneInfo.Utc));
        Assert.Contains(TicketBuilder.CancelledLegend, cancelledLines);
        Assert.DoesNotContain(TicketBuilder.ReprintLegend, cancelledLines);

        var both = Text(TicketBuilder.Build(Profile, cancelled, 32, new TicketOptions(IsReprint: true), TimeZoneInfo.Utc));
        Assert.Contains(TicketBuilder.CancelledLegend, both);
        Assert.Contains(TicketBuilder.ReprintLegend, both);

        var reprint = Text(TicketBuilder.Build(Profile, Simple(), 32, new TicketOptions(IsReprint: true), TimeZoneInfo.Utc));
        Assert.Contains(TicketBuilder.ReprintLegend, reprint);
        Assert.DoesNotContain(TicketBuilder.CancelledLegend, reprint);
    }

    [Fact]
    public void Encabezado_FolioFechaLocalYPie()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-6), "test", "test");

        var document = TicketBuilder.Build(Profile, Simple(), 32, timeZone: zone);
        var lines = Text(document);

        Assert.Equal("Mi Tienda", lines[0]);
        Assert.Equal(TicketAlignment.Center, document.Lines[0].Alignment);
        Assert.True(document.Lines[0].Bold);
        Assert.Contains("Tel. 555 123 4567", lines);
        Assert.Contains("RFC: XAXX010101000", lines);
        Assert.Contains("Folio: V-000123", lines);
        Assert.Contains("30/09/2026 08:05", lines);
        Assert.Equal("Gracias por su compra", lines[^1]);
        Assert.Equal("V-000123", document.Folio);
    }

    [Fact]
    public void Cajero_SaleBajoLaFechaYSeRecortaAlAnchoDelPapel()
    {
        var lines = Text(TicketBuilder.Build(Profile, Simple(), 32, timeZone: TimeZoneInfo.Utc));
        Assert.Equal("Cajero: Ana", lines[lines.IndexOf("30/09/2026 14:05") + 1]);

        var longName = Simple() with { CreatedByName = new string('M', 60) };
        var narrow = Text(TicketBuilder.Build(Profile, longName, 32, timeZone: TimeZoneInfo.Utc));
        Assert.Contains(narrow, l => l.StartsWith("Cajero: MMM", StringComparison.Ordinal) && l.Length == 32);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void TodosLosTickets_InicianConElMismoEncabezadoDelNegocio(int columns)
    {
        var sale = TicketBuilder.Build(Profile, Simple(), columns, timeZone: TimeZoneInfo.Utc).Lines;
        var header = sale.Take(4).ToList();
        Assert.Equal(["Mi Tienda", "Calle 1 #23, Col. Centro", "Tel. 555 123 4567", "RFC: XAXX010101000"], header.Select(l => l.Text.Trim()));
        Assert.True(header[0].Bold);

        TicketDocument[] others =
        [
            CreditNoteTicketBuilder.Build(Profile, new CreditNoteTicketData("NC-000001", 5000, Created, "V-000123"), columns, timeZone: TimeZoneInfo.Utc),
            CustomerPaymentReceiptBuilder.Build(
                Profile,
                new CustomerPaymentReceiptData("AB-000001", Created, "Juan", 1000, PaymentMethod.Cash, null, 3000, 2000, false),
                columns,
                timeZone: TimeZoneInfo.Utc),
            ShiftTicketBuilder.BuildMovementReceipt(
                Profile,
                new CashMovementReceiptDto(Guid.NewGuid(), "M-000001", CashMovementType.In, 1000, "Cambio", Created, "Ana", Guid.NewGuid(), Guid.NewGuid(), null),
                columns,
                timeZone: TimeZoneInfo.Utc),
        ];

        foreach (var other in others)
        {
            Assert.Equal(header, other.Lines.Take(header.Count));
        }
    }

    [Fact]
    public void SinPerfil_NoLlevaEncabezadoNiPie()
    {
        var lines = Text(TicketBuilder.Build(null, Simple(), 32, timeZone: TimeZoneInfo.Utc));

        Assert.Equal("Folio: V-000123", lines[0]);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void TicketDePrueba_LlevaFolioPruebaYRespetaElAncho(int columns)
    {
        var document = TicketBuilder.BuildSample(Profile, columns, TimeZoneInfo.Utc);

        Assert.Contains($"Folio: {TicketBuilder.SampleFolio}", Text(document));
        Assert.All(document.Lines, l => Assert.True(l.Text.Length <= columns));
    }

    [Theory]
    [InlineData("uno dos tres", 8, new[] { "uno dos", "tres" })]
    [InlineData("abcdefghij", 4, new[] { "abcd", "efgh", "ij" })]
    [InlineData("a\nb", 10, new[] { "a", "b" })]
    public void TextWrap_AjustaPorPalabrasYCortaLasLargas(string text, int width, string[] expected) =>
        Assert.Equal(expected, TextWrap.Wrap(text, width));

    [Fact]
    public void TextWrap_AlineaYColumnas()
    {
        Assert.Equal("   ab", TextWrap.Align("ab", 5, TicketAlignment.Right));
        Assert.Equal(" ab", TextWrap.Align("ab", 4, TicketAlignment.Center));
        Assert.Equal("AB   $1.00", TextWrap.TwoColumns("AB", "$1.00", 10));
    }
}
