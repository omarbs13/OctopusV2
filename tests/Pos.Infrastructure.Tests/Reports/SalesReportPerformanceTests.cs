using System.Diagnostics;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetSalesReport;
using Pos.Domain.CashShifts;
using Pos.Domain.Reports;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>SC-002 y SC-006: con 10,000 ventas los reportes responden en menos de 2 s y el PDF se genera en menos de 10 s.</summary>
public sealed class SalesReportPerformanceTests
{
    private const int Sales = 10_000;
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly ReportPeriod Period = ReportPeriod.Custom(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task VentasYArqueoResponden_EnMenosDeDosSegundos_YElPdfEnMenosDeDiez()
    {
        using var db = await TestDb.CreateAsync();
        var user = await ReportTestSupport.AddUserAsync(db, "cajero");
        Seed(db, user.Id);

        await using var context = db.CreateDbContext();
        var window = new SalesReportWindow(Resolver.Resolve(Period), Resolver.Days(Period), null);

        var watch = Stopwatch.StartNew();
        var report = await new SalesReportReader(context).GetAsync(window, new SalesReportQuery(Period), Ct);
        var salesTime = watch.Elapsed;

        watch.Restart();
        var shifts = await new CashCountReportReader(context).GetAsync(Resolver.Resolve(Period), null, Ct);
        var cashTime = watch.Elapsed;

        Assert.Equal(Sales, report.Totals.SalesCount);
        Assert.Equal(200, shifts.Count);
        Assert.True(salesTime < TimeSpan.FromSeconds(2), $"Ventas tardó {salesTime.TotalMilliseconds:N0} ms.");
        Assert.True(cashTime < TimeSpan.FromSeconds(2), $"Arqueo tardó {cashTime.TotalMilliseconds:N0} ms.");

        // PDF de todas las ventas del período: consulta sin paginar más escritura del archivo.
        watch.Restart();
        var all = await new SalesReportReader(context).GetAsync(window, new SalesReportQuery(Period, PageSize: ReportPaging.All), Ct);
        var document = new ReportDocument(
            "Reporte de ventas",
            "01/09/2026 - 30/09/2026",
            [],
            [new ReportMetric("Total vendido", new MoneyCell(all.Totals.TotalCents))],
            [
                new ReportTable(
                    "Detalle de ventas",
                    [
                        new ReportColumn("Folio", ReportColumnType.Text),
                        new ReportColumn("Fecha y hora", ReportColumnType.Date),
                        new ReportColumn("Cajero", ReportColumnType.Text),
                        new ReportColumn("Total", ReportColumnType.Money),
                    ],
                    [.. all.Rows.Select(r => (IReadOnlyList<ReportCell>)[new TextCell(r.FolioText), new DateCell(r.CreatedAtUtc), new TextCell(r.CashierName), new MoneyCell(r.TotalCents)])]),
            ],
            [new ChartSpec(ChartKind.Line, "Ventas por día", [.. all.Days.Select(d => new ChartPoint(d.LocalDate.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture), d.TotalCents))], ChartValueFormat.Money)],
            new ReportBusiness("Tienda", "Calle 1", "555"),
            DateTime.UtcNow,
            "Administrador");
        var pdf = new PdfReportWriter().Write(document);
        var pdfTime = watch.Elapsed;

        Assert.True(pdf.Length > 1_000);
        Assert.True(pdfTime < TimeSpan.FromSeconds(10), $"El PDF tardó {pdfTime.TotalSeconds:N1} s.");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Inserta 10,000 ventas con su pago y 200 turnos cerrados con SQL directo (sembrar con los casos de uso tardaría minutos).</summary>
    private static void Seed(TestDb db, Guid userId)
    {
        var id = userId.ToString().ToUpperInvariant();
        DatabaseTestHelpers.Execute(
            db.Directory.Paths.DatabaseFile,
            $"""
            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < {Sales})
            INSERT INTO Sales (Id, FolioNumber, DraftId, TotalCents, Status, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
            SELECT printf('00000000-0000-7000-8000-%012d', i), i, printf('11111111-0000-7000-8000-%012d', i), 1000 + (i % 500),
                   'COMPLETED', datetime('2026-09-01', '+' || (i % 30) || ' days', '+' || (i % 1440) || ' minutes'),
                   '{id}', '2026-09-30 00:00:00', '{id}', 1
            FROM n;

            INSERT INTO SalePayments (Id, SaleId, Method, AmountCents)
            SELECT printf('22222222-0000-7000-8000-%012d', FolioNumber), Id,
                   CASE FolioNumber % 3 WHEN 0 THEN 'CASH' WHEN 1 THEN 'CARD' ELSE 'TRANSFER' END, TotalCents
            FROM Sales;

            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 200)
            INSERT INTO CashShifts (Id, Number, RegisterCode, Status, OpenedBy, OpenedAt, OpeningFloatCents, ClosedAt, ClosedBy,
                                    SalesCount, CancelledCount, TotalSoldCents, CashSalesCents, CashCancelledCents, CardCents, TransferCents,
                                    DepositsCents, WithdrawalsCents, ExpectedCashCents, CountedCashCents, DifferenceCents,
                                    CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
            SELECT printf('33333333-0000-7000-8000-%012d', i), i, '{CashRegister.Default}', 'CLOSED', '{id}',
                   datetime('2026-09-01', '+' || (i % 30) || ' days', '+' || (i % 600) || ' minutes'), 50000,
                   datetime('2026-09-01', '+' || (i % 30) || ' days', '+' || (i % 600) || ' minutes', '+8 hours'), '{id}',
                   50, 0, 60000, 20000, 0, 20000, 20000, 0, 0, 70000, 70000 - (i % 7) * 100, -(i % 7) * 100,
                   '2026-09-30 00:00:00', '{id}', '2026-09-30 00:00:00', '{id}', 1
            FROM n;
            """);
    }
}
