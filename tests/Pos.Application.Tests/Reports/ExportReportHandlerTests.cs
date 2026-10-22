using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Products;
using Pos.Application.Reports;
using Pos.Application.Reports.Export;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Business;
using Pos.Domain.Inventory;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Reports;

public class ExportReportHandlerTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly ReportPeriod Period = ReportPeriod.Custom(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Arqueo_ElTurnoAbiertoNoLlevaEfectivoEsperadoContadoNiDiferencia()
    {
        var env = Environment.For(UserRole.Admin);
        env.CashRows =
        [
            Closed(expected: 100_000, counted: 94_000, difference: -6_000),
            new CashCountRawRow(Guid.NewGuid(), "T-000002", "Ana", DateTime.UtcNow, null, 5_000, 700, 0, 0, true, 1, 2, 3),
        ];

        var result = await env.Handler.HandleAsync(ExportRequest.ForCashCount(new CashCountReportQuery(Period), ExportFormat.Pdf), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var table = env.Pdf.Document!.Tables.Single();
        var open = table.Rows[1];
        Assert.IsType<TextCell>(open[3]);
        Assert.Equal(ReportTexts.InProgress, ((TextCell)open[3]).Text);
        Assert.All(open.Skip(8), cell => Assert.IsType<EmptyCell>(cell));
        Assert.Contains(table.Rows[0], c => c is MoneyCell { Cents: -6_000 });

        // El total vendido del turno abierto sí aparece; el efectivo no.
        Assert.DoesNotContain(open, c => c is MoneyCell { Cents: 1 or 2 or 3 });
    }

    [Fact]
    public async Task Inventario_IncluyeTodosLosRegistrosDelFiltroSinPaginar()
    {
        var env = Environment.For(UserRole.Cashier);
        env.Inventory = Enumerable.Range(1, 250)
            .Select(i => new InventoryReportRow(Guid.NewGuid(), $"Producto {i}", $"SKU-{i}", i * 1_000, null, null, "Pieza", 0, StockStatus.Normal, StockAlertLevel.None))
            .ToList();

        var result = await env.Handler.HandleAsync(ExportRequest.ForInventory(new InventoryReportQuery(new DateOnly(2026, 9, 30)), ExportFormat.Pdf), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(250, env.Pdf.Document!.Tables.Single().Rows.Count);
        Assert.Equal(ReportPaging.All, env.InventoryReader.LastQuery!.PageSize);
        Assert.Equal("inventario_2026-09-30.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task SinDatos_NoGeneraArchivoYSinPermisoSeRechaza()
    {
        var empty = Environment.For(UserRole.Admin);
        var none = await empty.Handler.HandleAsync(ExportRequest.ForSales(new SalesReportQuery(Period), ExportFormat.Pdf), TestContext.Current.CancellationToken);

        Assert.Equal(ReportMessages.NoData, Assert.IsType<InvalidState>(none.Error).Message);
        Assert.Equal(0, empty.Pdf.Writes);
        Assert.Empty(empty.Audit.Entries);

        var cashier = Environment.For(UserRole.Cashier);
        var denied = await cashier.Handler.HandleAsync(ExportRequest.ForSales(new SalesReportQuery(Period), ExportFormat.Pdf), TestContext.Current.CancellationToken);

        Assert.IsType<Forbidden>(denied.Error);
        Assert.Equal(0, cashier.Pdf.Writes);
    }

    [Fact]
    public async Task Exportar_RegistraLaBitacoraSinLosDatosExportadosYAvisaSiFaltaElNegocio()
    {
        var env = Environment.For(UserRole.Admin);
        env.CashRows = [Closed(expected: 100_000, counted: 94_000, difference: -6_000)];

        var result = await env.Handler.HandleAsync(ExportRequest.ForCashCount(new CashCountReportQuery(Period), ExportFormat.Xlsx), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.BusinessMissing);
        Assert.EndsWith(".xlsx", result.Value.FileName, StringComparison.Ordinal);
        var entry = Assert.Single(env.Audit.Entries);
        Assert.Equal("REPORT_EXPORTED", entry.Action);
        Assert.Contains("Arqueo", entry.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("94", entry.Details, StringComparison.Ordinal);

        env.Business = BusinessProfile.Create("Tienda", "Calle 1", "555", null, null);
        var withBusiness = await env.Handler.HandleAsync(ExportRequest.ForCashCount(new CashCountReportQuery(Period), ExportFormat.Pdf), TestContext.Current.CancellationToken);
        Assert.False(withBusiness.Value.BusinessMissing);
        Assert.Equal("Tienda", env.Pdf.Document!.Business!.Name);
    }

    private static CashCountRawRow Closed(long expected, long counted, long difference) =>
        new(Guid.NewGuid(), "T-000001", "Ana", DateTime.UtcNow, DateTime.UtcNow, 0, 10_000, 0, 0, false, expected, counted, difference);

    private sealed class Environment
    {
        private Environment()
        {
        }

        public ExportReportHandler Handler { get; private set; } = null!;

        public RecordingWriter Pdf { get; } = new();

        public RecordingAuditLog Audit { get; } = new();

        public InventoryReader InventoryReader { get; } = new();

        public IReadOnlyList<CashCountRawRow> CashRows { get; set; } = [];

        public IReadOnlyList<InventoryReportRow> Inventory
        {
            get => InventoryReader.Rows;
            set => InventoryReader.Rows = value;
        }

        public BusinessProfile? Business
        {
            get => BusinessRepository.Profile;
            set => BusinessRepository.Profile = value;
        }

        private FakeBusinessRepository BusinessRepository { get; } = new();

        public static Environment For(UserRole role)
        {
            var fixture = new AuthFixture();
            fixture.SignedIn(fixture.AddUser("usuario", role));
            var env = new Environment();
            var builder = new ReportDocumentBuilder(
                new GetSalesReportHandler(fixture.Access, new EmptySalesReader(), Resolver),
                new GetCashCountReportHandler(fixture.Access, new CashReader(env), Resolver, new Settings()),
                new GetInventoryReportHandler(fixture.Access, env.InventoryReader, Resolver),

                // "Mi turno" no se exporta en estas pruebas (se prueba con SQLite real en Infrastructure).
                new GetMyShiftSummaryHandler(fixture.Access, fixture.Session, null!, null!),
                env.BusinessRepository,
                fixture.Session,
                fixture.Clock);
            env.Handler = new ExportReportHandler(builder, env.Pdf, env.Pdf, env.Audit);
            return env;
        }
    }

    private sealed class RecordingWriter : IPdfReportWriter, IXlsxReportWriter
    {
        public int Writes { get; private set; }

        public ReportDocument? Document { get; private set; }

        public byte[] Write(ReportDocument document)
        {
            Writes++;
            Document = document;
            return [1, 2, 3];
        }
    }

    private sealed class EmptySalesReader : ISalesReportReader
    {
        public Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new SalesReport(SalesTotals.Empty, null, [], [], 0, 1, query.PageSize, []));
    }

    private sealed class CashReader(Environment env) : ICashCountReportReader
    {
        public Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken cancellationToken) =>
            Task.FromResult(env.CashRows);
    }

    private sealed class InventoryReader : IInventoryReportReader
    {
        public IReadOnlyList<InventoryReportRow> Rows { get; set; } = [];

        public InventoryReportQuery? LastQuery { get; private set; }

        public Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            var counts = new InventoryCounts(Rows.Count, Rows.Count, 0, 0, Rows.Count, 0, 0);
            return Task.FromResult(new InventoryReport(counts, Rows, Rows.Count, 1, query.PageSize));
        }
    }

    private sealed class Settings : IReportSettingsStore
    {
        public ReportSettings Load() => new();

        public void Save(ReportSettings settings)
        {
        }
    }

    private sealed class FakeBusinessRepository : IBusinessProfileRepository
    {
        public BusinessProfile? Profile { get; set; }

        public Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Profile);

        public void Add(BusinessProfile profile) => Profile = profile;

        public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(SaveOutcome.Saved);
    }
}
