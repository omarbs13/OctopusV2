using Pos.Application.Abstractions;
using Pos.Application.Receivables;
using Pos.Application.Reports.GetReceivablesReport;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Receivables;

/// <summary>014, Historia 4 y SC-006: "Reportes > Créditos" sobre SQLite real.</summary>
public sealed class ReceivablesReportTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private CreditTestSupport _credit = null!;
    private DateTime _today;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _credit = new CreditTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
        _today = _db.Clock.UtcNow;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Result<ReceivablesReport>> ReportAsync(ReceivablesStatusFilter? status = null, string? text = null)
    {
        await using var context = _db.CreateDbContext();
        return await new GetReceivablesReportHandler(_credit.Users.Access(context), new ReceivablesReportReader(context), _credit.Aging())
            .HandleAsync(new GetReceivablesReportQuery(status, text), Ct);
    }

    private async Task<Guid> PendingAtAsync(Guid customerId, long cents, int daysAgo)
    {
        _db.Clock.UtcNow = _today.AddDays(-daysAgo);
        var saleId = await _credit.InsertPendingReceivableAsync(customerId, cents);
        _db.Clock.UtcNow = _today;
        return saleId;
    }

    /// <summary>
    /// Ana: 300 de hace 40 días (vencida 10 con plazo 30) y 200 de hoy, con un abono de 50 hoy. Beto: 200 de
    /// hace 5 días con límite 200 (al límite, al día). Carla: pagó todo. Dora: nunca compró a crédito.
    /// </summary>
    private async Task<(Guid Ana, Guid Beto, Guid Carla, Guid Dora)> SeedAsync()
    {
        var ana = await _credit.CreditCustomerAsync("Ana", limitCents: 100_000);
        var beto = await _credit.CreditCustomerAsync("Beto", limitCents: 20_000);
        var carla = await _credit.CreditCustomerAsync("Carla", limitCents: 50_000);
        var dora = await _credit.CreditCustomerAsync("Dora", limitCents: 50_000);
        await PendingAtAsync(ana, 30_000, 40);
        await PendingAtAsync(ana, 20_000, 0);
        await PendingAtAsync(beto, 20_000, 5);
        await PendingAtAsync(carla, 10_000, 60);
        await SalesTestSupport.EnsureShiftAsync(_db);
        await _credit.PayOkAsync(ana, 5_000);
        await _credit.PayOkAsync(carla, 10_000);
        return (ana, beto, carla, dora);
    }

    [Fact]
    public async Task SoloClientesConSaldo_YElTotalEsLaSumaDeLosSaldosDeLasFichas()
    {
        var (ana, beto, carla, dora) = await SeedAsync();

        var report = (await ReportAsync()).Value;

        Assert.Equal([ana, beto], report.Rows.Select(r => r.CustomerId));
        Assert.Equal(2, report.CustomerCount);
        long sum = 0;
        foreach (var id in new[] { ana, beto, carla, dora })
        {
            sum += await _credit.BalanceAsync(id);
        }

        Assert.Equal(sum, report.TotalBalanceCents);
        Assert.Equal(65_000, report.TotalBalanceCents);
        var anaRow = report.Rows[0];
        Assert.Equal((45_000L, 100_000L, 10, true, false), (anaRow.BalanceCents, anaRow.LimitCents, anaRow.DaysOverdue, anaRow.IsOverdue, anaRow.IsAtLimit));
        Assert.Equal(_today, anaRow.LastPaymentAtUtc);
        Assert.Null(report.Rows[1].LastPaymentAtUtc);
    }

    [Fact]
    public async Task Filtros_VencidoYAlDiaSeExcluyen_YAlLimiteEsIndependiente()
    {
        var (ana, beto, _, _) = await SeedAsync();

        var overdue = (await ReportAsync(ReceivablesStatusFilter.Overdue)).Value;
        var current = (await ReportAsync(ReceivablesStatusFilter.Current)).Value;
        var atLimit = (await ReportAsync(ReceivablesStatusFilter.AtLimit)).Value;

        Assert.Equal([ana], overdue.Rows.Select(r => r.CustomerId));
        Assert.Equal([beto], current.Rows.Select(r => r.CustomerId));
        Assert.Equal([beto], atLimit.Rows.Select(r => r.CustomerId));
        Assert.Equal((45_000L, 1), (overdue.TotalBalanceCents, overdue.CustomerCount));
        Assert.Equal((20_000L, 1), (current.TotalBalanceCents, current.CustomerCount));
        Assert.Equal([beto], (await ReportAsync(text: "BÉTO")).Value.Rows.Select(r => r.CustomerId));
    }

    [Fact]
    public async Task DiasVencido_UsanElPlazoConfigurado()
    {
        var (_, beto, _, _) = await SeedAsync();
        _credit.Settings.Current = new ReceivablesSettings { PaymentTermDays = 1 };

        var report = (await ReportAsync()).Value;

        var row = report.Rows.Single(r => r.CustomerId == beto);
        Assert.Equal((4, true), (row.DaysOverdue, row.IsOverdue));
        Assert.Equal(1, report.PaymentTermDays);
    }

    [Fact]
    public async Task Cajero_NoVeElReporte()
    {
        _credit.Users.As(_credit.Users.Cashier);

        Assert.IsType<Forbidden>((await ReportAsync()).Error);
    }
}
