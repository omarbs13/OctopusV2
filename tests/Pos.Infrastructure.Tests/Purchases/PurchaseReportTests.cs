using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Reports.GetPurchaseReport;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Tests.TestSupport;
using static Pos.Infrastructure.Tests.TestSupport.PurchaseTestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>020, Historia 3: filtros, orden, paginación y acumulados del reporte de compras sobre SQLite real.</summary>
public sealed class PurchaseReportTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private PurchaseTestSupport _purchases = null!;
    private Guid _norte;
    private Guid _sur;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _purchases = await PurchaseTestSupport.CreateAsync(_db);
        _norte = await _purchases.Suppliers.CreateOkAsync("Norte");
        _sur = await _purchases.Suppliers.CreateOkAsync("Sur");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ConCientoCincuentaCompras_LosAcumuladosSonDeTodasLasPaginas_YLaPrimeraTraeCienEnOrden()
    {
        await SeedAsync(150);

        var result = await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, null, null, IncludeVoided: false));

        var page = result.Value;
        var all = await AllAsync();
        Assert.Equal(100, page.Rows.Count);
        Assert.Equal((150L, 2), (page.TotalCount, page.TotalPages));
        Assert.Equal((150L, all.Sum(p => p.SubtotalCents), all.Sum(p => p.TaxCents), all.Sum(p => p.TotalCents)), (page.PurchaseCount, page.SubtotalSumCents, page.TaxSumCents, page.TotalSumCents));
        Assert.Equal(
            all.OrderByDescending(p => p.InvoiceDate).ThenByDescending(p => p.CreatedAt).Take(100).Select(p => p.Id),
            page.Rows.Select(r => r.PurchaseId));
        Assert.Equal(50, (await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, null, null, false, Page: 2))).Value.Rows.Count);
    }

    [Fact]
    public async Task FiltrosCombinados_ProveedorInactivoFechasYTotal()
    {
        await SeedAsync(60);
        Assert.True((await _purchases.Suppliers.SetActiveAsync(_sur, active: false)).IsSuccess);
        var from = _purchases.Today.AddDays(-20);
        var to = _purchases.Today.AddDays(-5);

        var result = await _purchases.ReportAsync(new GetPurchaseReportQuery(_sur, from, to, "10.00", "40.00", IncludeVoided: false));

        var expected = (await AllAsync())
            .Where(p => p.SupplierId == _sur && p.InvoiceDate >= from && p.InvoiceDate <= to && p.TotalCents is >= 1_000 and <= 4_000)
            .ToList();
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Select(p => p.Id).Order(), result.Value.Rows.Select(r => r.PurchaseId).Order());
        Assert.Equal(expected.Sum(p => p.TotalCents), result.Value.TotalSumCents);
        Assert.All(result.Value.Rows, r => Assert.Equal("Sur", r.SupplierName));
    }

    [Fact]
    public async Task Anuladas_SeExcluyenPorOmision_YConIncluirSeListanSinSumarse()
    {
        var product = await _purchases.ProductAsync("REF");
        var kept = await _purchases.RegisterOkAsync(_norte, "F-1", Line(product, "1", "10.00"));
        var voided = await _purchases.RegisterOkAsync(_norte, "F-2", Line(product, "1", "20.00"));
        Assert.True((await _purchases.VoidAsync(voided.PurchaseId)).IsSuccess);

        var active = (await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, null, null, IncludeVoided: false))).Value;
        var all = (await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, null, null, IncludeVoided: true))).Value;

        Assert.Equal([kept.PurchaseId], active.Rows.Select(r => r.PurchaseId));
        Assert.Equal((2L, 1L, 1_000L), (all.TotalCount, all.PurchaseCount, all.TotalSumCents));
        Assert.True(all.Rows.Single(r => r.PurchaseId == voided.PurchaseId).IsVoided);
        Assert.Equal(active.TotalSumCents, all.TotalSumCents);
    }

    [Fact]
    public async Task RangoSinCompras_AcumuladosEnCero()
    {
        await SeedAsync(3);

        var result = await _purchases.ReportAsync(new GetPurchaseReportQuery(null, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31), null, null, false));

        Assert.Empty(result.Value.Rows);
        Assert.Equal((0L, 0L, 0L, 0L, 0L), (result.Value.TotalCount, result.Value.PurchaseCount, result.Value.SubtotalSumCents, result.Value.TaxSumCents, result.Value.TotalSumCents));
    }

    [Fact]
    public async Task FiltroInvalido_EsErrorPorCampo()
    {
        var today = _purchases.Today;

        var dates = await _purchases.ReportAsync(new GetPurchaseReportQuery(null, today, today.AddDays(-1), null, null, false));
        var range = await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, "50.00", "10.00", false));
        var negative = await _purchases.ReportAsync(new GetPurchaseReportQuery(null, null, null, "-1", "abc", false));

        Assert.Equal([ReportFields.ToDate], Assert.IsType<ValidationFailed>(dates.Error).Errors.Select(e => e.Field));
        Assert.Equal([ReportFields.MaxTotal], Assert.IsType<ValidationFailed>(range.Error).Errors.Select(e => e.Field));
        Assert.Equal([ReportFields.MinTotal, ReportFields.MaxTotal], Assert.IsType<ValidationFailed>(negative.Error).Errors.Select(e => e.Field));
    }

    /// <summary>Compras alternadas entre los dos proveedores, en fechas y con totales distintos; una por segundo.</summary>
    private async Task SeedAsync(int count)
    {
        var product = await _purchases.ProductAsync("SEED");
        for (var i = 0; i < count; i++)
        {
            _db.Clock.Advance(TimeSpan.FromSeconds(1));
            var supplier = i % 2 == 0 ? _norte : _sur;
            var result = await _purchases.RegisterAsync(
                supplier,
                $"F-{i:000}",
                [Line(product, (1 + (i % 7)).ToString(System.Globalization.CultureInfo.InvariantCulture), "5.00")],
                tax: (i % 3).ToString(System.Globalization.CultureInfo.InvariantCulture),
                date: _purchases.Today.AddDays(-(i % 30)));
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }
    }

    private async Task<List<Purchase>> AllAsync()
    {
        await using var context = _db.CreateDbContext();
        return await context.Purchases.AsNoTracking().Where(p => p.Status == PurchaseStatus.Active).ToListAsync(Ct);
    }
}
