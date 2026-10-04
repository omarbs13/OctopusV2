using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Sales;
using Pos.Domain.CashShifts;
using Pos.Domain.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.CashShifts;

/// <summary>
/// Reglas de integridad de los turnos (venta ligada, cancelación, movimientos y cierre) con los casos de
/// uso reales sobre SQLite (plan.md, research §16).
/// </summary>
public sealed class CashShiftUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _shifts = null!;
    private Product _product = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _shifts = await ShiftTestSupport.CreateAsync(_db);
        _product = await SalesTestSupport.SeedProductAsync(_db, "TUR-1", tracks: false, priceCents: 10_000);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Result<ConfirmedSale>> ConfirmDirectAsync()
    {
        await using var context = _db.CreateDbContext();
        return await SalesTestSupport.ConfirmHandler(_db, context)
            .HandleAsync(SalesTestSupport.CashSale(Guid.CreateVersion7(), (_product, 1000)), Ct);
    }

    private async Task<ConfirmedSale> SellAsync()
    {
        var result = await ConfirmDirectAsync();
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    // --- Ventas ligadas al turno (SC-001) ---

    [Fact]
    public async Task Venta_QuedaLigadaAlTurnoPropio()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync()).Value;

        var sale = await SellAsync();

        await using var context = _db.CreateDbContext();
        var stored = await context.Sales.AsNoTracking().SingleAsync(s => s.Id == sale.SaleId, Ct);
        Assert.Equal(shift.ShiftId, stored.CashShiftId);
        Assert.Equal(_shifts.Cashier.Id, stored.CreatedBy);
    }

    [Fact]
    public async Task Venta_SeRechazaSinTurnoYConElTurnoDeOtroUsuario()
    {
        _shifts.As(_shifts.Cashier);
        Assert.IsType<ShiftRequired>((await ConfirmDirectAsync()).Error);

        _shifts.As(_shifts.Admin);
        await _shifts.OpenAsync();
        _shifts.As(_shifts.Cashier);

        var other = Assert.IsType<ShiftOwnedByOther>((await ConfirmDirectAsync()).Error);
        Assert.Equal(_shifts.Admin.UserName, other.OpenedByName);
    }

    // --- Cancelación dentro del turno (FR-008) ---

    [Fact]
    public async Task Cancelacion_EnElTurnoAbiertoSeAceptaYDescuentaElEfectivo()
    {
        _shifts.As(_shifts.Admin);
        var shift = (await _shifts.OpenAsync(0, confirmZero: true)).Value;
        var sale = await SellAsync();

        Assert.True((await _shifts.CancelAsync(sale.SaleId)).IsSuccess);

        var count = await _shifts.CountAsync(shift.ShiftId, 0);
        Assert.Equal(0, count.Value.ExpectedCents);
    }

    [Fact]
    public async Task Cancelacion_DeVentaDeTurnoCerradoOSinTurnoSeRechaza()
    {
        _shifts.As(_shifts.Admin);
        var first = (await _shifts.OpenAsync(0, confirmZero: true)).Value;
        var sale = await SellAsync();
        var count = await _shifts.CountAsync(first.ShiftId, 10_000);
        Assert.True((await _shifts.CloseAsync(first.ShiftId, count.Value.Version, 10_000, count.Value.ExpectedCents)).IsSuccess);
        await _shifts.OpenAsync(0, confirmZero: true);

        Assert.IsType<InvalidState>((await _shifts.CancelAsync(sale.SaleId)).Error);

        // Venta anterior a 0.6.0: sin turno.
        await using (var context = _db.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE Sales SET CashShiftId = NULL", Ct);
        }

        Assert.IsType<InvalidState>((await _shifts.CancelAsync(sale.SaleId)).Error);
    }

    [Fact]
    public async Task Cancelacion_ConEfectivoInsuficienteSeRechazaSinRevelarMontos()
    {
        _shifts.As(_shifts.Admin);
        var shift = (await _shifts.OpenAsync(0, confirmZero: true)).Value;
        var sale = await SellAsync();
        Assert.True((await _shifts.MoveAsync(shift.ShiftId, CashMovementType.Out, 10_000)).IsSuccess);

        var result = await _shifts.CancelAsync(sale.SaleId);

        Assert.Null(Assert.IsType<InsufficientCash>(result.Error).AvailableCents);
    }

    // --- Movimientos (FR-009 a FR-011) ---

    [Fact]
    public async Task Ingreso_DelCajeroAumentaElEsperadoYQuedaEnLaBitacora()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;

        var moved = await _shifts.MoveAsync(shift.ShiftId, CashMovementType.In, 5_000, "Más cambio");

        Assert.True(moved.IsSuccess);
        Assert.EndsWith("-01", moved.Value.Folio, StringComparison.Ordinal);
        Assert.Equal(15_000, (await _shifts.CountAsync(shift.ShiftId, 15_000)).Value.ExpectedCents);
        await using var context = _db.CreateDbContext();
        Assert.Single(await context.AuditEntries.Where(a => a.Action == AuditActions.CashDeposit).ToListAsync(Ct));
    }

    [Fact]
    public async Task Retiro_DelCajeroExigeAutorizacionYLaConcesionQuedaEnLaBitacora()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;

        var denied = await _shifts.MoveAsync(shift.ShiftId, CashMovementType.Out, 1_000);
        Assert.True(Assert.IsType<Forbidden>(denied.Error).CanBeAuthorized);

        var grant = _shifts.Grants.Issue(Pos.Domain.Users.Permission.WithdrawCash, _shifts.Cashier.Id, _shifts.Admin.Id);
        var allowed = await _shifts.MoveAsync(shift.ShiftId, CashMovementType.Out, 1_000, grant: grant);

        Assert.True(allowed.IsSuccess);
        await using var context = _db.CreateDbContext();
        var entry = await context.AuditEntries.AsNoTracking().SingleAsync(a => a.Action == AuditActions.CashWithdrawal, Ct);
        Assert.Equal(_shifts.Admin.Id, entry.AuthorizedBy);
    }

    [Fact]
    public async Task Retiro_ExcedenteSeRechazaYSoloElAdministradorVeElMontoDisponible()
    {
        _shifts.As(_shifts.Admin);
        var shift = (await _shifts.OpenAsync(10_000)).Value;

        var asAdmin = await _shifts.MoveAsync(shift.ShiftId, CashMovementType.Out, 10_001);
        Assert.Equal(10_000, Assert.IsType<InsufficientCash>(asAdmin.Error).AvailableCents);

        _shifts.As(_shifts.Cashier);
        var grant = _shifts.Grants.Issue(Pos.Domain.Users.Permission.WithdrawCash, _shifts.Cashier.Id, _shifts.Admin.Id);
        var asCashier = await _shifts.MoveAsync(shift.ShiftId, CashMovementType.Out, 10_001, grant: grant);
        Assert.IsType<Forbidden>(asCashier.Error);
    }

    [Fact]
    public async Task Movimiento_SinMotivoOConMontoNoPositivoSeRechaza()
    {
        _shifts.As(_shifts.Admin);
        var shift = (await _shifts.OpenAsync()).Value;

        Assert.IsType<ValidationFailed>((await _shifts.MoveAsync(shift.ShiftId, CashMovementType.In, 100, " ")).Error);
        Assert.IsType<ValidationFailed>((await _shifts.MoveAsync(shift.ShiftId, CashMovementType.In, 0)).Error);
    }

    // --- Apertura y cierre ---

    [Fact]
    public async Task Apertura_ConFondoCeroExigeConfirmacionYUnSegundoTurnoSeRechaza()
    {
        _shifts.As(_shifts.Admin);

        Assert.IsType<ValidationFailed>((await _shifts.OpenAsync(0)).Error);
        Assert.True((await _shifts.OpenAsync(0, confirmZero: true)).IsSuccess);
        Assert.IsType<ShiftAlreadyOpen>((await _shifts.OpenAsync(1_000)).Error);
    }

    [Fact]
    public async Task Cierre_PropioCuadradoQuedaInmutableYAuditado()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;
        await SellAsync();
        var count = (await _shifts.CountAsync(shift.ShiftId, 20_000)).Value;
        Assert.Equal(20_000, count.ExpectedCents);
        Assert.Equal(DifferenceKind.Balanced, count.DifferenceKind);

        var closed = await _shifts.CloseAsync(shift.ShiftId, count.Version, 20_000, count.ExpectedCents);

        Assert.True(closed.IsSuccess);
        Assert.IsType<ShiftClosed>((await _shifts.CountAsync(shift.ShiftId, 20_000)).Error);
        Assert.IsType<ShiftRequired>((await ConfirmDirectAsync()).Error);
        await using var context = _db.CreateDbContext();
        Assert.Single(await context.AuditEntries.Where(a => a.Action == AuditActions.ShiftClosed).ToListAsync(Ct));
    }

    [Fact]
    public async Task Cierre_ConDiferenciaExigeComentarioYSiElEsperadoCambioPideRevisar()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;
        var count = (await _shifts.CountAsync(shift.ShiftId, 9_000)).Value;
        Assert.Equal(DifferenceKind.Shortage, count.DifferenceKind);

        Assert.IsType<ValidationFailed>((await _shifts.CloseAsync(shift.ShiftId, count.Version, 9_000, count.ExpectedCents)).Error);
        Assert.IsType<ShiftChanged>((await _shifts.CloseAsync(shift.ShiftId, count.Version, 9_000, count.ExpectedCents + 1, "Faltó")).Error);
        Assert.True((await _shifts.CloseAsync(shift.ShiftId, count.Version, 9_000, count.ExpectedCents, "Faltó")).IsSuccess);
    }

    [Fact]
    public async Task Cierre_ConVentaEnCursoDelDuenoSeRechazaAntesDeRevelarCifras()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;
        await _shifts.SaveDraftAsync(_product);

        Assert.IsType<SaleInProgress>((await _shifts.CountAsync(shift.ShiftId, 10_000)).Error);
    }

    [Fact]
    public async Task Cierre_DeTurnoAjenoPorAdministradorDescartaLaVentaConservadaConConfirmacionYAudita()
    {
        _shifts.As(_shifts.Cashier);
        var shift = (await _shifts.OpenAsync(10_000)).Value;
        await _shifts.SaveDraftAsync(_product);

        // Un cajero no puede cerrar el turno de otro, aunque el suyo no lo sea.
        _shifts.As(_shifts.Admin);
        var pending = await _shifts.CountAsync(shift.ShiftId, 10_000);
        Assert.Equal(_shifts.Cashier.UserName, Assert.IsType<HeldSaleWillBeDiscarded>(pending.Error).OwnerName);

        var count = (await _shifts.CountAsync(shift.ShiftId, 10_000, discard: true)).Value;
        var closed = await _shifts.CloseAsync(shift.ShiftId, count.Version, 10_000, count.ExpectedCents, discard: true);

        Assert.True(closed.IsSuccess);
        await using var context = _db.CreateDbContext();
        Assert.Empty(await context.SaleDrafts.ToListAsync(Ct));
        Assert.Single(await context.AuditEntries.Where(a => a.Action == AuditActions.HeldSaleDiscarded).ToListAsync(Ct));
        var byAdmin = await context.AuditEntries.AsNoTracking().SingleAsync(a => a.Action == AuditActions.ShiftClosedByAdmin, Ct);
        Assert.Contains("Dueño: " + _shifts.Cashier.UserName, byAdmin.Details, StringComparison.Ordinal);
        Assert.Empty(await context.AuditEntries.Where(a => a.Action == AuditActions.ShiftClosed).ToListAsync(Ct));
        Assert.Equal(_shifts.Admin.Id, (await context.CashShifts.AsNoTracking().SingleAsync(Ct)).ClosedBy);
    }

    [Fact]
    public async Task Cierre_DeTurnoAjenoPorUnCajeroSeRechaza()
    {
        _shifts.As(_shifts.Admin);
        var shift = (await _shifts.OpenAsync(10_000)).Value;
        _shifts.As(_shifts.Cashier);

        var result = await _shifts.CountAsync(shift.ShiftId, 10_000);

        Assert.Equal(Pos.Domain.Users.Permission.ManageShifts, Assert.IsType<Forbidden>(result.Error).Permission);
    }
}
