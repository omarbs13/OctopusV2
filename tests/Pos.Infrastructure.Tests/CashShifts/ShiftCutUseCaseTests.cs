using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.SearchShiftCuts;
using Pos.Domain.CashShifts;
using Pos.Domain.Products;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.CashShifts;

/// <summary>
/// Cortes X y Z (017) con los casos de uso reales sobre SQLite: el Corte X no altera el turno, los
/// folios son consecutivos por tipo sin huecos y el histórico filtra y pagina (plan.md, research §14).
/// </summary>
public sealed class ShiftCutUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _shifts = null!;
    private Product _product = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _shifts = await ShiftTestSupport.CreateAsync(_db);
        _product = await SalesTestSupport.SeedProductAsync(_db, "COR-1", tracks: false, priceCents: 10_000);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SellAsync()
    {
        await using var context = _db.CreateDbContext();
        var result = await SalesTestSupport.ConfirmHandler(_db, context)
            .HandleAsync(SalesTestSupport.CashSale(Guid.CreateVersion7(), (_product, 1000)), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    private async Task<CashShift> ReadShiftAsync(Guid shiftId)
    {
        await using var context = _db.CreateDbContext();
        return await context.CashShifts.AsNoTracking().Include(s => s.Movements).SingleAsync(s => s.Id == shiftId, Ct);
    }

    private async Task<long> ExpectedAsync(Guid shiftId)
    {
        await using var context = _db.CreateDbContext();
        var shift = await context.CashShifts.AsNoTracking().Include(s => s.Movements).SingleAsync(s => s.Id == shiftId, Ct);
        return shift.ExpectedCash(await new Pos.Infrastructure.Sales.SaleRepository(context).GetShiftTotalsAsync(shiftId, Ct));
    }

    private async Task<Result<ClosedShift>> CloseAsync(Guid shiftId, long countedCents = -1, string? comment = null)
    {
        var shift = await ReadShiftAsync(shiftId);
        var expected = await ExpectedAsync(shiftId);
        return await _shifts.CloseAsync(shiftId, shift.Version, countedCents < 0 ? expected : countedCents, expected, comment);
    }

    // --- Historia 1: Corte X ---

    [Fact]
    public async Task CorteX_NoCambiaElTurnoYSuInstantaneaQuedaFija()
    {
        var shift = (await _shifts.OpenAsync(50_000)).Value;
        await SellAsync();
        Assert.True((await _shifts.MoveAsync(shift.ShiftId, CashMovementType.In, 5_000)).IsSuccess);
        var before = await ReadShiftAsync(shift.ShiftId);
        var expectedBefore = await ExpectedAsync(shift.ShiftId);

        var cut = (await _shifts.ReadoutAsync()).Value;

        var after = await ReadShiftAsync(shift.ShiftId);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(CashShiftStatus.Open, after.Status);
        Assert.Null(after.ExpectedCashCents);
        Assert.Equal(expectedBefore, await ExpectedAsync(shift.ShiftId));

        var report = (await _shifts.GetCutAsync(cut.CutId)).Value;
        Assert.Equal(ShiftCutType.Readout, report.Type);
        Assert.Equal(expectedBefore, report.ExpectedCashCents);
        Assert.Equal(1, report.SalesCount);
        Assert.Equal(10_000, report.TotalSoldCents);
        Assert.Equal(5_000, report.DepositsCents);
        Assert.Null(report.CountedCashCents);
        Assert.Null(report.DifferenceCents);

        // Escenarios 1.4 y 3.4: otra venta entra en el siguiente Corte X; el primero no cambia.
        await SellAsync();
        var second = (await _shifts.GetCutAsync((await _shifts.ReadoutAsync()).Value.CutId)).Value;
        Assert.Equal(2, second.SalesCount);
        Assert.Equal(expectedBefore + 10_000, second.ExpectedCashCents);
        Assert.Equal(report, (await _shifts.GetCutAsync(cut.CutId)).Value);
    }

    [Fact]
    public async Task CorteX_FoliosConsecutivosYAuditados()
    {
        var shift = (await _shifts.OpenAsync(50_000)).Value;

        var first = (await _shifts.ReadoutAsync()).Value;
        var second = (await _shifts.ReadoutAsync()).Value;

        Assert.Equal("X-000001", first.Folio);
        Assert.Equal("X-000002", second.Folio);
        Assert.Equal(shift.Folio, first.ShiftFolio);
        await using var context = _db.CreateDbContext();
        var audit = await context.AuditEntries.AsNoTracking()
            .Where(a => a.Action == AuditActions.ShiftReadoutGenerated)
            .ToListAsync(Ct);
        Assert.Equal(2, audit.Count);
        Assert.All(audit, a => Assert.Equal(AuditActions.ShiftCutEntity, a.EntityType));
        Assert.Contains(audit, a => a.Details!.StartsWith($"Corte X X-000001. Turno {shift.Folio}.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorteX_CajeroRequiereAutorizacionYQuedaQuienAutorizo()
    {
        _shifts.As(_shifts.Cashier);
        await _shifts.OpenAsync(10_000);

        var denied = Assert.IsType<Forbidden>((await _shifts.ReadoutAsync()).Error);
        Assert.True(denied.CanBeAuthorized);

        var grant = _shifts.Grants.Issue(Permission.GenerateShiftReadout, _shifts.Cashier.Id, _shifts.Admin.Id);
        var cut = (await _shifts.ReadoutAsync(grant)).Value;

        Assert.Equal("X-000001", cut.Folio);
        var report = (await _shifts.GetCutAsync(cut.CutId)).Value;
        Assert.Equal(_shifts.Cashier.Id, report.GeneratedById);
        Assert.Equal(_shifts.Admin.UserName, report.AuthorizedByName);
        await using var context = _db.CreateDbContext();
        var audit = await context.AuditEntries.AsNoTracking().SingleAsync(a => a.Action == AuditActions.ShiftReadoutGenerated, Ct);
        Assert.Equal(_shifts.Admin.Id, audit.AuthorizedBy);
    }

    [Fact]
    public async Task CorteX_SinTurnoAbiertoSeRechazaYNoConsumeFolio()
    {
        Assert.IsType<ShiftRequired>((await _shifts.ReadoutAsync()).Error);

        await _shifts.OpenAsync(10_000);
        Assert.Equal("X-000001", (await _shifts.ReadoutAsync()).Value.Folio);
    }

    // --- Historia 2: Corte Z ---

    [Fact]
    public async Task CorteZ_FoliosConsecutivosYUnCierreRechazadoNoConsumeFolio()
    {
        var first = (await _shifts.OpenAsync(10_000)).Value;
        var shift = await ReadShiftAsync(first.ShiftId);
        var expected = await ExpectedAsync(first.ShiftId);
        Assert.IsType<ShiftChanged>((await _shifts.CloseAsync(first.ShiftId, shift.Version, expected, expected + 1)).Error);

        var closed = (await CloseAsync(first.ShiftId, expected - 100, "Faltó")).Value;
        Assert.Equal("Z-000001", closed.CutFolio);

        var second = (await _shifts.OpenAsync(10_000)).Value;
        Assert.Equal("Z-000002", (await CloseAsync(second.ShiftId)).Value.CutFolio);

        var report = (await _shifts.GetCutAsync(closed.CutId)).Value;
        Assert.Equal(ShiftCutType.Closing, report.Type);
        Assert.Equal(expected - 100, report.CountedCashCents);
        Assert.Equal(-100, report.DifferenceCents);
        Assert.Equal("Faltó", report.Comment);
        await using var context = _db.CreateDbContext();
        Assert.Equal(2, await context.ShiftCuts.CountAsync(c => c.Type == ShiftCutType.Closing, Ct));
        var audit = await context.AuditEntries.AsNoTracking().Where(a => a.Action == AuditActions.ShiftClosed).ToListAsync(Ct);
        Assert.Contains(audit, a => a.Details!.StartsWith($"Corte Z Z-000001. Turno {first.Folio}.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorteZ_DosCierresSimultaneosSoloUnoSeCompletaSinHuecos()
    {
        var opened = (await _shifts.OpenAsync(10_000)).Value;
        var shift = await ReadShiftAsync(opened.ShiftId);
        var expected = await ExpectedAsync(opened.ShiftId);

        var results = await Task.WhenAll(
            Task.Run(() => _shifts.CloseAsync(opened.ShiftId, shift.Version, expected, expected), Ct),
            Task.Run(() => _shifts.CloseAsync(opened.ShiftId, shift.Version, expected, expected), Ct));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.Error is ShiftClosed or Conflict);
        var next = (await _shifts.OpenAsync(10_000)).Value;
        Assert.Equal("Z-000002", (await CloseAsync(next.ShiftId)).Value.CutFolio);
    }

    [Fact]
    public async Task CorteX_DespuesDeCorteZYTurnoNuevoSoloIncluyeElTurnoNuevo()
    {
        var first = (await _shifts.OpenAsync(10_000)).Value;
        await SellAsync();
        Assert.True((await CloseAsync(first.ShiftId)).IsSuccess);

        await _shifts.OpenAsync(30_000);
        var report = (await _shifts.GetCutAsync((await _shifts.ReadoutAsync()).Value.CutId)).Value;

        Assert.Equal(0, report.SalesCount);
        Assert.Equal(0, report.TotalSoldCents);
        Assert.Equal(30_000, report.ExpectedCashCents);
    }

    // --- Historia 3: Histórico ---

    [Fact]
    public async Task Historico_FiltraPorTipoYPaginaDelMasRecienteAlMasAntiguo()
    {
        _db.Clock.UtcNow = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var opened = (await _shifts.OpenAsync(10_000)).Value;
        for (var i = 0; i < ShiftCutPage.DefaultPageSize; i++)
        {
            _db.Clock.Advance(TimeSpan.FromSeconds(10));
            Assert.True((await _shifts.ReadoutAsync()).IsSuccess);
        }

        _db.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True((await CloseAsync(opened.ShiftId)).IsSuccess);

        var all = (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, null, null, null))).Value;
        Assert.Equal(ShiftCutPage.DefaultPageSize + 1, all.TotalCount);
        Assert.Equal(2, all.TotalPages);
        Assert.Equal(ShiftCutPage.DefaultPageSize, all.Items.Count);
        Assert.Equal("Z-000001", all.Items[0].Folio);
        Assert.Equal("X-000100", all.Items[1].Folio);
        Assert.Equal(all.Items.OrderByDescending(c => c.GeneratedAtUtc).ThenByDescending(c => c.Id), all.Items);

        var last = (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, null, null, null, Page: 2))).Value;
        Assert.Equal("X-000001", Assert.Single(last.Items).Folio);

        var closing = (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(ShiftCutType.Closing, null, null, null))).Value;
        var z = Assert.Single(closing.Items);
        Assert.Equal(0, z.DifferenceCents);
        Assert.Equal(_shifts.Admin.UserName, z.GeneratedByName);

        var day = new DateOnly(2026, 10, 1);
        var byDay = (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(ShiftCutType.Readout, day, day, _shifts.Admin.Id))).Value;
        Assert.Equal(ShiftCutPage.DefaultPageSize, byDay.TotalCount);
        Assert.Equal(0, (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, day.AddDays(1), null, null))).Value.TotalCount);
        Assert.Equal(0, (await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, null, null, _shifts.Cashier.Id))).Value.TotalCount);
    }

    [Fact]
    public async Task Historico_RangoInvertidoSeRechazaYElCajeroNoTieneAcceso()
    {
        var day = new DateOnly(2026, 10, 1);
        Assert.IsType<ValidationFailed>((await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, day, day.AddDays(-1), null))).Error);

        _shifts.As(_shifts.Cashier);
        Assert.IsType<Forbidden>((await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, null, null, null))).Error);
    }
}
