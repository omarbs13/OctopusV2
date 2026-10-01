using Pos.Application.Reports;
using Pos.Domain.Reports;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

public sealed class CashCountReportReaderTests
{
    private static readonly ReportPeriodResolver Resolver = new(TimeZoneInfo.Utc);
    private static readonly ReportPeriod Period = ReportPeriod.Custom(new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task TurnoCerradoTraeLaInstantaneaYElAbiertoNoTraeCifrasDeEfectivo()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await SeedAsync(db);

        var rows = await GetAsync(db, null);

        Assert.Equal(3, rows.Count);
        var shortage = rows[0];
        Assert.Equal((100_000L, 94_000L, -6_000L), (shortage.ExpectedCashCents, shortage.CountedCashCents, shortage.DifferenceCents));
        Assert.False(shortage.IsOpen);
        Assert.Equal(shifts.Cashier.FullName, shortage.CashierName);

        var surplus = rows[1];
        Assert.Equal(2_000, surplus.DifferenceCents);

        var open = rows[2];
        Assert.True(open.IsOpen);
        Assert.Null(open.ClosedAtUtc);
        Assert.Null(open.ExpectedCashCents);
        Assert.Null(open.CountedCashCents);
        Assert.Null(open.DifferenceCents);
        Assert.Equal(1_000, open.TotalSoldCents);
        Assert.Equal(50_000, open.OpeningFloatCents);
    }

    [Fact]
    public async Task FiltroPorCajeroYTurnoAsignadoPorSuApertura()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await SeedAsync(db);

        var cashierRows = await GetAsync(db, shifts.Cashier.Id);
        var adminRows = await GetAsync(db, shifts.Admin.Id);

        Assert.Equal(2, cashierRows.Count);
        Assert.Single(adminRows);

        // Un período que no contiene las aperturas no trae turnos.
        await using var context = db.CreateDbContext();
        var empty = await new CashCountReportReader(context).GetAsync(
            Resolver.Resolve(ReportPeriod.Custom(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10))),
            null,
            TestContext.Current.CancellationToken);
        Assert.Empty(empty);
    }

    private static async Task<IReadOnlyList<Pos.Application.Reports.GetCashCountReport.CashCountRawRow>> GetAsync(TestDb db, Guid? cashierId)
    {
        await using var context = db.CreateDbContext();
        return await new CashCountReportReader(context).GetAsync(Resolver.Resolve(Period), cashierId, TestContext.Current.CancellationToken);
    }

    private static async Task<ShiftTestSupport> SeedAsync(TestDb db)
    {
        var shifts = await ShiftTestSupport.CreateAsync(db);
        var product = await ReportTestSupport.SeedProductAsync(db);

        // Faltante: esperado 1,000.00, contado 940.00.
        shifts.As(shifts.Cashier);
        db.Clock.UtcNow = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc);
        var first = (await shifts.OpenAsync(100_000)).Value;
        var count = (await shifts.CountAsync(first.ShiftId, 94_000)).Value;
        db.Clock.UtcNow = new DateTime(2026, 9, 28, 17, 0, 0, DateTimeKind.Utc);
        Assert.True((await shifts.CloseAsync(first.ShiftId, count.Version, 94_000, count.ExpectedCents, "Faltante")).IsSuccess);

        // Sobrante de +20.00.
        db.Clock.UtcNow = new DateTime(2026, 9, 29, 9, 0, 0, DateTimeKind.Utc);
        var second = (await shifts.OpenAsync(100_000)).Value;
        count = (await shifts.CountAsync(second.ShiftId, 102_000)).Value;
        db.Clock.UtcNow = new DateTime(2026, 9, 29, 17, 0, 0, DateTimeKind.Utc);
        Assert.True((await shifts.CloseAsync(second.ShiftId, count.Version, 102_000, count.ExpectedCents, "Sobrante")).IsSuccess);

        // Turno abierto del administrador con una venta.
        shifts.As(shifts.Admin);
        db.Clock.UtcNow = new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc);
        Assert.True((await shifts.OpenAsync(50_000)).IsSuccess);
        await SalesTestSupport.SellOkAsync(db, (product, 1_000));
        return shifts;
    }
}
