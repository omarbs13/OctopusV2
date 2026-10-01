using Pos.Application.Abstractions;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Reports.ListMyShifts;
using Pos.Domain.CashShifts;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Reports;

/// <summary>Historia 6 con SQLite real y permisos reales: el Cajero solo ve su turno y el arqueo sigue ciego mientras está abierto.</summary>
public sealed class MyShiftTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TurnoAbierto_NoTraeEsperadoNiContado_YElCerradoSi()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await ShiftTestSupport.CreateAsync(db);
        var product = await ReportTestSupport.SeedProductAsync(db);

        shifts.As(shifts.Cashier);
        var open = (await shifts.OpenAsync(10_000)).Value;
        Assert.True((await shifts.MoveAsync(open.ShiftId, CashMovementType.In, 2_000, "Cambio")).IsSuccess);
        await SalesTestSupport.SellOkAsync(db, (product, 1_000));

        var summary = (await GetAsync(db, shifts, null)).Value;

        Assert.True(summary.IsOpen);
        Assert.Equal(10_000, summary.OpeningFloatCents);
        Assert.Equal(1, summary.SalesCount);
        Assert.Equal(1_000, summary.TotalSoldCents);
        Assert.Equal(2_000, summary.DepositsCents);
        Assert.Single(summary.Movements);
        Assert.Null(summary.ExpectedCashCents);
        Assert.Null(summary.CountedCashCents);
        Assert.Null(summary.DifferenceCents);

        var count = (await shifts.CountAsync(open.ShiftId, 12_500)).Value;
        Assert.True((await shifts.CloseAsync(open.ShiftId, count.Version, 12_500, count.ExpectedCents, "Faltante")).IsSuccess);

        var closed = (await GetAsync(db, shifts, open.ShiftId)).Value;
        Assert.False(closed.IsOpen);
        Assert.Equal(13_000, closed.ExpectedCashCents);
        Assert.Equal(12_500, closed.CountedCashCents);
        Assert.Equal(-500, closed.DifferenceCents);
    }

    [Fact]
    public async Task ElCajeroNoVeTurnosAjenos_NiEnElDetalleNiEnLaLista()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await ShiftTestSupport.CreateAsync(db);

        shifts.As(shifts.Admin);
        var adminShift = (await shifts.OpenAsync(5_000)).Value;
        var count = (await shifts.CountAsync(adminShift.ShiftId, 5_000)).Value;
        Assert.True((await shifts.CloseAsync(adminShift.ShiftId, count.Version, 5_000, count.ExpectedCents)).IsSuccess);

        shifts.As(shifts.Cashier);
        var mine = (await shifts.OpenAsync(7_000)).Value;

        // Pedir el turno del administrador como Cajero responde "no encontrado".
        Assert.IsType<NotFound>((await GetAsync(db, shifts, adminShift.ShiftId)).Error);

        var list = (await ListAsync(db, shifts)).Value;
        Assert.Equal([mine.ShiftId], list.Select(i => i.ShiftId));
        Assert.True(list[0].IsOpen);
    }

    [Fact]
    public async Task SinTurnoAbiertoPropio_DevuelveNoEncontradoYLaListaTraeSusCerrados()
    {
        using var db = await TestDb.CreateAsync();
        var shifts = await ShiftTestSupport.CreateAsync(db);

        // El turno abierto es del administrador: el Cajero no tiene uno abierto.
        shifts.As(shifts.Admin);
        await shifts.OpenAsync(1_000);
        shifts.As(shifts.Cashier);

        Assert.IsType<NotFound>((await GetAsync(db, shifts, null)).Error);
        Assert.Empty((await ListAsync(db, shifts)).Value);
    }

    private static async Task<Result<MyShiftSummary>> GetAsync(TestDb db, ShiftTestSupport shifts, Guid? shiftId)
    {
        await using var context = db.CreateDbContext();
        return await new GetMyShiftSummaryHandler(shifts.Access(context), db.User, new CashShiftRepository(context), new SaleRepository(context))
            .HandleAsync(shiftId, Ct);
    }

    private static async Task<Result<IReadOnlyList<MyShiftListItem>>> ListAsync(TestDb db, ShiftTestSupport shifts)
    {
        await using var context = db.CreateDbContext();
        return await new ListMyShiftsHandler(shifts.Access(context), db.User, new CashShiftRepository(context)).HandleAsync(Ct);
    }
}
