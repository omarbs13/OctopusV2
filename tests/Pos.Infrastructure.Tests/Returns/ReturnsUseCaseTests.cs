using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CreditNotes.GetCreditNoteBalance;
using Pos.Application.CreditNotes.SearchCreditNotes;
using Pos.Application.Returns;
using Pos.Application.Returns.MarkReversalDone;
using Pos.Application.Returns.PreviewReturn;
using Pos.Application.Returns.SearchPendingReversals;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.CashShifts;
using Pos.Domain.CreditNotes;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Returns;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Returns;

/// <summary>
/// 013: cancelaciones, devoluciones parciales, auditoría y notas de crédito sobre SQLite real con
/// usuarios, permisos y concesiones reales (Principio VI: dinero, integridad y concurrencia).
/// </summary>
public sealed class ReturnsUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _users = null!;
    private ReturnsTestSupport _returns = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _users = await ShiftTestSupport.CreateAsync(_db);
        _returns = new ReturnsTestSupport(_db, _users);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<Product> ProductAsync(string sku, long priceCents = 10_000, bool tracks = true) =>
        SalesTestSupport.SeedProductAsync(_db, sku, tracks: tracks, priceCents: priceCents);

    private async Task<(long Expected, ShiftSalesTotals Totals)> OpenShiftNumbersAsync()
    {
        await using var context = _db.CreateDbContext();
        var shift = (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!;
        var totals = await new SaleRepository(context).GetShiftTotalsAsync(shift.Id, Ct);
        return (shift.ExpectedCash(totals), totals);
    }

    private async Task<SaleReturn> LoadReturnAsync(Guid saleId)
    {
        await using var context = _db.CreateDbContext();
        return await context.SaleReturns.AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Refunds)
            .OrderByDescending(r => r.Number)
            .FirstAsync(r => r.SaleId == saleId, Ct);
    }

    // ---------------------------------------------------------------- Historia 1: cancelar con autorización

    [Fact]
    public async Task Cancelar_ConMotivoVacio_SeRechazaSinCambios()
    {
        var product = await ProductAsync("R-1");
        var sale = await _returns.SellAsync(product, 1000);

        var result = await _returns.CancelAsync(sale.SaleId, reason: "   ");

        Assert.IsType<ValidationFailed>(result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Completed, (await check.Sales.AsNoTracking().SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task Cancelar_SinConcesion_EsForbiddenAutorizableTambienParaElAdministrador()
    {
        var product = await ProductAsync("R-2");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await _returns.SellAsync(product, 4000);

        // Quien opera es Administrador: aun así necesita la concesión con su contraseña (research §7).
        var result = await _returns.CancelAsync(sale.SaleId, authorized: false);

        var forbidden = Assert.IsType<Forbidden>(result.Error);
        Assert.Equal(Permission.ApproveReturns, forbidden.Permission);
        Assert.True(forbidden.CanBeAuthorized);
        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Completed, (await check.Sales.AsNoTracking().SingleAsync(Ct)).Status);
        Assert.Equal(6_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Empty(check.SaleReturns);
    }

    [Fact]
    public async Task Cancelar_ConReintegroEnEfectivo_DescuentaDelEsperadoDelTurnoYLoAnotaEnLaBitacora()
    {
        var product = await ProductAsync("R-3");
        var sale = await _returns.SellAsync(product, 1000);
        Assert.Equal(10_000, (await OpenShiftNumbersAsync()).Expected);

        var result = await _returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var saleReturn = await LoadReturnAsync(sale.SaleId);
        Assert.Equal(ReturnKind.Cancellation, saleReturn.Kind);
        var refund = Assert.Single(saleReturn.Refunds);
        Assert.Equal((PaymentMethod.Cash, 10_000L, RefundStatus.Paid), (refund.Method, refund.AmountCents, refund.Status));
        var (expected, totals) = await OpenShiftNumbersAsync();
        Assert.Equal(0, expected);
        Assert.Equal(10_000, totals.CashRefundsCents);
        // Con devolución registrada no se aplica además el efectivo cancelado heredado.
        Assert.Equal(0, totals.CashCancelledCents);
    }

    [Fact]
    public async Task Cancelar_VentaConTarjeta_AnotaReintegroPendienteSinTocarElEfectivo()
    {
        var product = await ProductAsync("R-4");
        var sale = await _returns.SellAsync(product, 1000, (PaymentMethod.Card, 10_000, null));
        var before = (await OpenShiftNumbersAsync()).Expected;

        var result = await _returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var refund = Assert.Single((await LoadReturnAsync(sale.SaleId)).Refunds);
        Assert.Equal((PaymentMethod.Card, RefundStatus.PendingReversal), (refund.Method, refund.Status));
        var (expected, totals) = await OpenShiftNumbersAsync();
        Assert.Equal(before, expected);
        Assert.Equal(0, totals.CashRefundsCents);
        Assert.Equal(10_000, totals.NonCashRefundsCents);
    }

    [Fact]
    public async Task Cancelar_PagoMixto_RepartePorElPesoDeCadaFormaDePagoConSumaExacta()
    {
        var product = await ProductAsync("R-5", priceCents: 10_001);
        var sale = await _returns.SellAsync(product, 1000, (PaymentMethod.Card, 4_001, null));

        var result = await _returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var saleReturn = await LoadReturnAsync(sale.SaleId);
        Assert.Equal(10_001, saleReturn.Refunds.Sum(r => r.AmountCents));
        Assert.Equal(6_000, saleReturn.Refunds.Single(r => r.Method == PaymentMethod.Cash).AmountCents);
        Assert.Equal(4_001, saleReturn.Refunds.Single(r => r.Method == PaymentMethod.Card).AmountCents);
    }

    [Fact]
    public async Task Cancelar_ConNotaDeCredito_CreaElValePorElTotalSinMoverEfectivo()
    {
        var product = await ProductAsync("R-6");
        var sale = await _returns.SellAsync(product, 1000);
        var before = (await OpenShiftNumbersAsync()).Expected;

        var result = await _returns.CancelAsync(sale.SaleId, ReturnCompensation.CreditNote);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal("NC-000001", result.Value.CreditNoteFolio);
        await using var check = _db.CreateDbContext();
        var note = await check.CreditNotes.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(10_000, note.InitialCents);
        Assert.Equal(10_000, await new CreditNoteRepository(check).GetBalanceAsync(note.Id, Ct));
        Assert.Empty(check.SaleReturnRefunds);
        var (expected, totals) = await OpenShiftNumbersAsync();
        Assert.Equal(before, expected);
        Assert.Equal(10_000, totals.CreditNotesIssuedCents);
        Assert.Equal(0, totals.CashRefundsCents);
    }

    [Fact]
    public async Task Cancelar_UnaVentaYaCancelada_SeRechaza()
    {
        var product = await ProductAsync("R-7");
        var sale = await _returns.SellAsync(product, 1000);
        await _returns.CancelAsync(sale.SaleId);

        var again = await _returns.CancelAsync(sale.SaleId);

        Assert.IsType<InvalidState>(again.Error);
        await using var check = _db.CreateDbContext();
        Assert.Single(check.SaleReturns);
    }

    [Fact]
    public async Task Cancelar_DosVecesAlMismoTiempo_SoloUnaTieneExitoSinDuplicarInventarioNiDinero()
    {
        var product = await ProductAsync("R-8");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await _returns.SellAsync(product, 4000);

        var results = await Task.WhenAll(_returns.CancelAsync(sale.SaleId), _returns.CancelAsync(sale.SaleId));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        await using var check = _db.CreateDbContext();
        Assert.Single(check.SaleReturns);
        Assert.Equal(10_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Equal(40_000, (await OpenShiftNumbersAsync()).Totals.CashRefundsCents);
    }

    [Fact]
    public async Task Cancelar_FueraDelPlazo_SeRechazaYElAdministradorPuedeAmpliarlo()
    {
        var product = await ProductAsync("R-9");
        var sale = await _returns.SellAsync(product, 1000);
        _db.Clock.Advance(TimeSpan.FromDays(31));

        var expired = await _returns.CancelAsync(sale.SaleId);

        Assert.Equal(30, Assert.IsType<ReturnWindowExpired>(expired.Error).Days);

        _returns.Settings.Current = new ReturnsSettings { ReturnWindowDays = 60 };
        // El turno sigue abierto y es del mismo usuario, así que el reintegro en efectivo procede.
        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);
    }

    [Fact]
    public async Task Cancelar_VentaDeTurnoCerrado_ReintegraDesdeElTurnoAbiertoActualYNoModificaElCerrado()
    {
        var product = await ProductAsync("R-10");
        var sale = await _returns.SellAsync(product, 1000);
        Guid closedShiftId;
        int closedVersion;
        await using (var context = _db.CreateDbContext())
        {
            var shift = (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!;
            closedShiftId = shift.Id;
            closedVersion = shift.Version;
            var close = await _users.CloseAsync(shift.Id, shift.Version, 10_000, 10_000);
            Assert.True(close.IsSuccess, close.Error?.ToString());
        }

        Assert.True((await _users.OpenAsync(floatCents: 20_000)).IsSuccess);

        var result = await _returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        await using var check = _db.CreateDbContext();
        var closed = await check.CashShifts.AsNoTracking().SingleAsync(s => s.Id == closedShiftId, Ct);
        Assert.Equal(closedVersion + 1, closed.Version);
        Assert.Equal(0, closed.CashRefundsCents);
        var saleReturn = await check.SaleReturns.AsNoTracking().SingleAsync(Ct);
        Assert.NotEqual(closedShiftId, saleReturn.CashShiftId);
        var (expected, totals) = await OpenShiftNumbersAsync();
        Assert.Equal(10_000, totals.CashRefundsCents);
        Assert.Equal(10_000, expected);
    }

    [Fact]
    public async Task Cancelar_SinTurnoAbierto_NoHayReintegroEnEfectivoPeroSiEnTarjeta()
    {
        var product = await ProductAsync("R-11");
        var cashSale = await _returns.SellAsync(product, 1000);
        var cardSale = await _returns.SellAsync(product, 1000, (PaymentMethod.Card, 10_000, null));
        await using (var context = _db.CreateDbContext())
        {
            var shift = (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!;
            var close = await _users.CloseAsync(shift.Id, shift.Version, 10_000, 10_000);
            Assert.True(close.IsSuccess, close.Error?.ToString());
        }

        var cash = await _returns.CancelAsync(cashSale.SaleId);
        var card = await _returns.CancelAsync(cardSale.SaleId);

        Assert.IsType<ShiftRequired>(cash.Error);
        Assert.True(card.IsSuccess, card.Error?.ToString());
    }

    [Fact]
    public async Task Cancelar_ConEfectivoInsuficiente_SeRechazaSinRevelarMontos()
    {
        var product = await ProductAsync("R-12");
        var sale = await _returns.SellAsync(product, 1000);
        var shiftId = (await _users.MoveAsync(await OpenShiftIdAsync(), CashMovementType.Out, 10_000)).IsSuccess;
        Assert.True(shiftId);

        var result = await _returns.CancelAsync(sale.SaleId);

        Assert.Null(Assert.IsType<InsufficientCash>(result.Error).AvailableCents);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.SaleReturns);
    }

    private async Task<Guid> OpenShiftIdAsync()
    {
        await using var context = _db.CreateDbContext();
        return (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!.Id;
    }

    [Fact]
    public async Task Cancelar_SiFallaElGuardado_NoQuedaNingunCambioParcial()
    {
        var product = await ProductAsync("R-13");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var first = await _returns.SellAsync(product, 1000);
        var second = await _returns.SellAsync(product, 2000);
        Assert.True((await _returns.CancelAsync(first.SaleId)).IsSuccess);

        // Un folio repetido hace fallar el guardado después de armar venta, inventario y nota.
        _returns.ReturnsFactory = context => new DuplicateNumberRepository(new ReturnRepository(context));
        var result = await _returns.CancelAsync(second.SaleId, ReturnCompensation.CreditNote);

        Assert.IsType<Conflict>(result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Completed, (await check.Sales.AsNoTracking().SingleAsync(s => s.Id == second.SaleId, Ct)).Status);
        Assert.Single(check.SaleReturns);
        Assert.Empty(check.CreditNotes);
        Assert.Empty(check.CreditNoteMovements);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == AuditActions.SaleCancelled, Ct));
        // Existencia: inicial 10, dos ventas (1 y 2) y una cancelación (1) = 8.
        Assert.Equal(8_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
    }

    [Fact]
    public async Task Cancelar_ConElModuloDevolucionesInactivo_ConservaLaCancelacionBasica()
    {
        var product = await ProductAsync("R-14");
        var sale = await _returns.SellAsync(product, 1000);
        _users.License = _returns.Modular(LicensedModule.Inventory, LicensedModule.CashShifts);

        var cancel = await _returns.CancelAsync(sale.SaleId, authorized: false);
        var partial = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(await _returns.LineIdAsync(sale.SaleId, 1), 500)]);

        Assert.True(cancel.IsSuccess, cancel.Error?.ToString());
        Assert.Equal(Guid.Empty, cancel.Value.ReturnId);
        Assert.IsType<ModuleNotLicensed>(partial.Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.SaleReturns);
        Assert.Equal(SaleStatus.Cancelled, (await check.Sales.AsNoTracking().SingleAsync(Ct)).Status);
    }

    // ---------------------------------------------------------------- Historia 2: devolución parcial

    [Fact]
    public async Task Parcial_ConCantidadMayorALaDisponibleOSinLineas_SeRechaza()
    {
        var product = await ProductAsync("P-1");
        var sale = await _returns.SellAsync(product, 2000);
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        var tooMuch = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 2001)]);
        var none = await _returns.ReturnAsync(sale.SaleId, []);

        Assert.IsType<NothingToReturn>(tooMuch.Error);
        Assert.IsType<ValidationFailed>(none.Error);
    }

    [Fact]
    public async Task Parcial_AcumulaHastaAgotarLoVendido_YLaVentaSigueVigenteConSuDetalle()
    {
        var product = await ProductAsync("P-2", priceCents: 1_000);
        var sale = await _returns.SellAsync(product, 3000);
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        var first = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1000)]);
        var second = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 2000)]);
        var excess = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1)]);

        Assert.Equal(1_000, first.Value.TotalCents);
        Assert.Equal(2_000, second.Value.TotalCents);
        Assert.False(excess.IsSuccess);
        await using var check = _db.CreateDbContext();
        var stored = await check.Sales.AsNoTracking().Include(s => s.Lines).SingleAsync(Ct);
        Assert.Equal(SaleStatus.Completed, stored.Status);
        Assert.True(stored.IsFullyReturned);
        Assert.Equal(3_000, stored.ReturnedCents);
        Assert.Equal(3_000, stored.Lines.Single().ReturnedQuantity);
        Assert.Equal(3_000, stored.Lines.Single().QuantityThousandths);
        Assert.Equal(3_000, stored.TotalCents);
    }

    [Fact]
    public async Task Parcial_PagoMixto_RepartePorPesoConTopesPorPagoHastaDevolverTodoExacto()
    {
        var product = await ProductAsync("P-3", priceCents: 5_000);
        var sale = await _returns.SellAsync(product, 2000, (PaymentMethod.Card, 4_000, null));
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1000)]);
        var rest = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1000)]);

        Assert.True(rest.IsSuccess, rest.Error?.ToString());
        await using var check = _db.CreateDbContext();
        var refunds = await check.SaleReturnRefunds.AsNoTracking().ToListAsync(Ct);
        // Pagado: $60.00 en efectivo y $40.00 con tarjeta; devolver todo reintegra exactamente eso.
        Assert.Equal(6_000, refunds.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.AmountCents));
        Assert.Equal(4_000, refunds.Where(r => r.Method == PaymentMethod.Card).Sum(r => r.AmountCents));
    }

    [Fact]
    public async Task Parcial_TrasUnaDevolucion_NoSePuedeCancelarLaVentaCompleta()
    {
        var product = await ProductAsync("P-4");
        var sale = await _returns.SellAsync(product, 2000);
        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(await _returns.LineIdAsync(sale.SaleId, 1), 1000)]);

        var cancel = await _returns.CancelAsync(sale.SaleId);

        Assert.IsType<InvalidState>(cancel.Error);
    }

    [Fact]
    public async Task Parcial_DosDevolucionesAlMismoTiempoSobreLaMismaLinea_NoExcedenLoVendido()
    {
        var product = await ProductAsync("P-5");
        var sale = await _returns.SellAsync(product, 2000);
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        var results = await Task.WhenAll(
            _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1500)]),
            _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1500)]));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        await using var check = _db.CreateDbContext();
        Assert.Equal(1_500, (await check.SaleLines.AsNoTracking().SingleAsync(Ct)).ReturnedQuantity);
    }

    [Fact]
    public async Task Parcial_LosTotalesDelTurnoYDeInicioQuedanNetosDeLaDevolucion()
    {
        var product = await ProductAsync("P-6", priceCents: 1_000);
        var sale = await _returns.SellAsync(product, 3000);
        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(await _returns.LineIdAsync(sale.SaleId, 1), 1000)]);

        var (_, totals) = await OpenShiftNumbersAsync();
        await using var context = _db.CreateDbContext();
        var day = _db.Clock.UtcNow.Date;
        var dashboard = await new SaleRepository(context).GetDashboardAsync(
            [new DayWindow(DateOnly.FromDateTime(day), day, day.AddDays(1))],
            Ct);

        Assert.Equal(2_000, totals.TotalSoldCents);
        Assert.Equal(2_000, dashboard.Days.Single().TotalCents);
        Assert.Equal(1_000, totals.CashRefundsCents);
        // El importe de la fila de la venta sigue siendo el original (research §11).
        var page = await new SaleRepository(context).SearchAsync(new SaleSearch(null, null, null, null, 1, 100), Ct);
        Assert.Equal(3_000, page.Items.Single().TotalCents);
    }

    [Fact]
    public async Task Vista_Previa_CalculaMontoYRepartoSinGuardarNada()
    {
        var product = await ProductAsync("P-7", priceCents: 5_000);
        var sale = await _returns.SellAsync(product, 2000, (PaymentMethod.Card, 4_000, null));
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        await using var context = _db.CreateDbContext();
        var access = _users.Access(context);
        var handler = new PreviewReturnHandler(
            access,
            _db.User,
            new SaleRepository(context),
            new ReturnRepository(context),
            _returns.Settings,
            new ReturnCashGate(new CashShiftRepository(context), new SaleRepository(context), access, _db.User, _users.Guard(context)),
            _db.Clock);
        var preview = await handler.HandleAsync(new PreviewReturnCommand(sale.SaleId, [new ReturnLineRequest(line, 1000)]), Ct);

        Assert.True(preview.IsSuccess, preview.Error?.ToString());
        Assert.Equal(5_000, preview.Value.TotalCents);
        Assert.Equal(3_000, preview.Value.CashRefundCents);
        Assert.True(preview.Value.CanRefundCash);
        Assert.True(preview.Value.WithinWindow);
        Assert.Equal(5_000, preview.Value.RefundBreakdown.Sum(b => b.AmountCents));
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.SaleReturns);
    }

    // ---------------------------------------------------------------- Historia 3: auditoría e inmutabilidad

    [Fact]
    public async Task Auditoria_RegistraUsuarioMotivoAutorizadorVentaMontoYCompensacion()
    {
        _users.As(_users.Cashier);
        var product = await ProductAsync("A-1");
        var sale = await _returns.SellAsync(product, 1000);

        var result = await _returns.CancelAsync(sale.SaleId, ReturnCompensation.CreditNote, "Cliente se arrepintió");

        Assert.True(result.IsSuccess, result.Error?.ToString());
        await using var check = _db.CreateDbContext();
        var entry = await check.AuditEntries.AsNoTracking().SingleAsync(a => a.Action == AuditActions.SaleCancelled, Ct);
        Assert.Equal(sale.SaleId, entry.EntityId);
        Assert.Equal(_users.Cashier.Id, entry.CreatedBy);
        Assert.Equal(_users.Admin.Id, entry.AuthorizedBy);
        Assert.Contains("Cliente se arrepintió", entry.Details, StringComparison.Ordinal);
        Assert.Contains(sale.Folio, entry.Details, StringComparison.Ordinal);
        Assert.Contains("$100.00", entry.Details, StringComparison.Ordinal);
        Assert.Contains("Nota de crédito NC-000001", entry.Details, StringComparison.Ordinal);
        var saleReturn = await check.SaleReturns.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((_users.Cashier.Id, _users.Admin.Id), (saleReturn.CreatedBy, saleReturn.AuthorizedBy));
        Assert.Equal(_db.Clock.UtcNow, saleReturn.CreatedAt);
    }

    [Fact]
    public async Task Historial_ElDetalleDeLaVentaMuestraLosEventosConLaVentaOriginalSinCambios()
    {
        var product = await ProductAsync("A-2", priceCents: 1_000);
        var sale = await _returns.SellAsync(product, 3000);
        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(await _returns.LineIdAsync(sale.SaleId, 1), 1000)], reason: "Sobrante");

        await using var context = _db.CreateDbContext();
        var detail = (await new SaleRepository(context).GetDetailAsync(sale.SaleId, Ct))!;

        Assert.Equal(3_000, detail.TotalCents);
        Assert.Equal(3_000, detail.Lines.Single().QuantityThousandths);
        Assert.Equal(1_000, detail.Lines.Single().ReturnedThousandths);
        Assert.Equal(1_000, detail.ReturnedCents);
        Assert.True(detail.IsPartiallyReturned);
        var history = Assert.Single(detail.ReturnHistory);
        Assert.Equal(("D-000001", "Sobrante", "Admin Uno"), (history.Folio, history.Reason, history.AuthorizedByName));
    }

    [Fact]
    public async Task Reversa_SoloUnaVezLaMarcaElAdministradorYQuedaEnLaBitacora()
    {
        var product = await ProductAsync("A-3");
        var sale = await _returns.SellAsync(product, 1000, (PaymentMethod.Card, 10_000, null));
        await _returns.CancelAsync(sale.SaleId);
        Guid refundId;
        await using (var context = _db.CreateDbContext())
        {
            var page = await new SearchPendingReversalsHandler(_users.Access(context), new ReturnRepository(context))
                .HandleAsync(new SearchPendingReversalsQuery(ReversalFilter.Pending, 1), Ct);
            var item = Assert.Single(page.Value.Items);
            Assert.Equal((PaymentMethod.Card, 10_000L), (item.Method, item.AmountCents));
            refundId = item.RefundId;
        }

        var first = await MarkAsync(refundId);
        var second = await MarkAsync(refundId);

        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.IsType<InvalidState>(second.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(RefundStatus.Reversed, (await check.SaleReturnRefunds.AsNoTracking().SingleAsync(Ct)).Status);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == AuditActions.CardReversalDone, Ct));
        var pending = await new ReturnRepository(check).SearchReversalsAsync(new ReversalSearch(ReversalFilter.Pending, 1, 100), Ct);
        Assert.Empty(pending.Items);
    }

    private async Task<Result> MarkAsync(Guid refundId)
    {
        await using var context = _db.CreateDbContext();
        return await new MarkReversalDoneHandler(
            _users.Access(context),
            new ReturnRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            NullLogger<MarkReversalDoneHandler>.Instance).HandleAsync(refundId, Ct);
    }

    [Fact]
    public async Task Inmutabilidad_NoSePuedeEditarNiBorrarDevolucionesNiNotas()
    {
        var product = await ProductAsync("A-4");
        var sale = await _returns.SellAsync(product, 1000);
        await _returns.CancelAsync(sale.SaleId, ReturnCompensation.CreditNote);

        await using (var context = _db.CreateDbContext())
        {
            var saleReturn = await context.SaleReturns.SingleAsync(Ct);
            context.Entry(saleReturn).Property(r => r.Reason).CurrentValue = "Otro motivo";
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateDbContext())
        {
            context.SaleReturnLines.Remove(await context.SaleReturnLines.FirstAsync(Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateDbContext())
        {
            context.CreditNotes.Remove(await context.CreditNotes.SingleAsync(Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateDbContext())
        {
            var movement = await context.CreditNoteMovements.SingleAsync(Ct);
            context.Entry(movement).Property(m => m.AmountCents).CurrentValue = 1;
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task Consistencia_LosAcumuladosDeLaVentaCoincidenConLaSumaDeLasLineasDevueltas()
    {
        var product = await ProductAsync("A-5", priceCents: 1_003);
        var other = await ProductAsync("A-6", priceCents: 2_500);
        var sale = await _returns.SellLinesAsync((product, 3000), (other, 2000));
        var first = await _returns.LineIdAsync(sale.SaleId, 1);
        var second = await _returns.LineIdAsync(sale.SaleId, 2);

        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(first, 1000), new ReturnLineRequest(second, 500)]);
        await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(first, 1000)]);

        await using var check = _db.CreateDbContext();
        var stored = await check.Sales.AsNoTracking().Include(s => s.Lines).SingleAsync(Ct);
        var returned = await check.SaleReturnLines.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(returned.Sum(l => l.AmountCents), stored.ReturnedCents);
        foreach (var line in stored.Lines)
        {
            Assert.Equal(returned.Where(l => l.SaleLineId == line.Id).Sum(l => l.QuantityThousandths), line.ReturnedQuantity);
        }
    }

    [Fact]
    public async Task Permisos_SoloManageCreditNotesListaNotasYReintegrosYElCajeroNo()
    {
        var product = await ProductAsync("A-7");
        var sale = await _returns.SellAsync(product, 1000, (PaymentMethod.Card, 10_000, null));
        await _returns.CancelAsync(sale.SaleId);
        _users.As(_users.Cashier);

        await using var context = _db.CreateDbContext();
        var access = _users.Access(context);
        var pending = await new SearchPendingReversalsHandler(access, new ReturnRepository(context))
            .HandleAsync(new SearchPendingReversalsQuery(ReversalFilter.All, 1), Ct);
        var notes = await new SearchCreditNotesHandler(access, new CreditNoteRepository(context))
            .HandleAsync(new SearchCreditNotesQuery(null, false, 1), Ct);
        var mark = await MarkAsync(Guid.NewGuid());

        Assert.IsType<Forbidden>(pending.Error);
        Assert.IsType<Forbidden>(notes.Error);
        Assert.IsType<Forbidden>(mark.Error);
    }

    // ---------------------------------------------------------------- Historia 4: nota de crédito como pago

    private async Task<string> IssueNoteAsync(long amountCents)
    {
        var product = await ProductAsync($"NC-{Guid.NewGuid():N}"[..12], priceCents: amountCents, tracks: false);
        var sale = await _returns.SellAsync(product, 1000);
        var result = await _returns.CancelAsync(sale.SaleId, ReturnCompensation.CreditNote);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value.CreditNoteFolio!;
    }

    private async Task<long> BalanceAsync(string folio)
    {
        await using var context = _db.CreateDbContext();
        Assert.True(CreditNoteFolio.TryParse(folio, out var number));
        var note = await context.CreditNotes.AsNoTracking().SingleAsync(n => n.Number == number, Ct);
        return await new CreditNoteRepository(context).GetBalanceAsync(note.Id, Ct);
    }

    [Fact]
    public async Task Nota_PagarUnaVentaMenorDescuentaElTotalDeLaVenta()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-1", priceCents: 6_000, tracks: false);

        var sale = await _returns.TrySellWithNoteAsync(product, 1000, folio, 10_000);

        Assert.True(sale.IsSuccess, sale.Error?.ToString());
        Assert.Equal(4_000, await BalanceAsync(folio));
        await using var check = _db.CreateDbContext();
        var payment = await check.SalePayments.AsNoTracking().SingleAsync(p => p.Method == PaymentMethod.CreditNote, Ct);
        Assert.Equal(6_000, payment.AmountCents);
        Assert.NotNull(payment.CreditNoteId);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == AuditActions.CreditNoteRedeemed, Ct));
    }

    [Fact]
    public async Task Nota_ConUnaVentaMayorAlSaldoSeCombinaConEfectivoYElSaldoQuedaEnCero()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-2", priceCents: 15_000, tracks: false);

        var sale = await _returns.TrySellWithNoteAsync(product, 1000, folio, 10_000);

        Assert.True(sale.IsSuccess, sale.Error?.ToString());
        Assert.Equal(0, await BalanceAsync(folio));
        await using var check = _db.CreateDbContext();
        Assert.Equal(5_000, (await check.SalePayments.AsNoTracking().SingleAsync(p => p.Method == PaymentMethod.Cash && p.SaleId == sale.Value.SaleId, Ct)).AmountCents);
    }

    [Fact]
    public async Task Nota_FolioInexistenteSinSaldoOSaldoInsuficiente_SeRechazaSinCambios()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-3", priceCents: 20_000, tracks: false);

        var unknown = await _returns.TrySellWithNoteAsync(product, 1000, "NC-000099", 20_000);
        var insufficient = await _returns.TrySellWithNoteAsync(product, 1000, folio, 20_000);
        var empty = await ProductAsync("N-4", priceCents: 10_000, tracks: false);
        Assert.True((await _returns.TrySellWithNoteAsync(empty, 1000, folio, 10_000)).IsSuccess);
        var spent = await _returns.TrySellWithNoteAsync(empty, 1000, folio, 10_000);

        Assert.IsType<CreditNoteNotFound>(unknown.Error);
        Assert.Equal(10_000, Assert.IsType<InsufficientCreditNote>(insufficient.Error).AvailableCents);
        Assert.IsType<CreditNoteNotFound>(spent.Error);
        Assert.Equal(0, await BalanceAsync(folio));
    }

    [Fact]
    public async Task Nota_DosCobrosSimultaneosConLaMismaNota_NoGastanElSaldoDosVeces()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-5", priceCents: 8_000, tracks: false);
        await SalesTestSupport.EnsureShiftAsync(_db);

        var results = await Task.WhenAll(
            _returns.TrySellWithNoteAsync(product, 1000, folio, 8_000),
            _returns.TrySellWithNoteAsync(product, 1000, folio, 8_000));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(2_000, await BalanceAsync(folio));
    }

    [Fact]
    public async Task Nota_ALoMasUnPagoConNotaPorVenta()
    {
        var folio = await IssueNoteAsync(10_000);
        var other = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-6", priceCents: 10_000, tracks: false);
        await SalesTestSupport.EnsureShiftAsync(_db);

        await using var context = _db.CreateDbContext();
        var result = await _returns.ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, 1000, product.Price.Cents)],
                [
                    new PaymentInput(PaymentMethod.CreditNote, 5_000, null, folio),
                    new PaymentInput(PaymentMethod.CreditNote, 5_000, null, other),
                ]),
            Ct);

        Assert.IsType<ValidationFailed>(result.Error);
    }

    [Fact]
    public async Task Nota_SinLicenciaDeDevoluciones_NoSePuedeUsarComoPago()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-7", priceCents: 5_000, tracks: false);
        _users.License = _returns.Modular(LicensedModule.CashShifts, LicensedModule.Inventory);

        var sale = await _returns.TrySellWithNoteAsync(product, 1000, folio, 5_000);

        Assert.Equal(LicensedModule.Returns, Assert.IsType<ModuleNotLicensed>(sale.Error).Module);
        Assert.Equal(10_000, await BalanceAsync(folio));
    }

    [Fact]
    public async Task Nota_CancelarUnaVentaPagadaConNotaRestauraElSaldoNuncaComoEfectivo_YLosSaldosCuadran()
    {
        var folio = await IssueNoteAsync(10_000);
        var product = await ProductAsync("N-8", priceCents: 6_000, tracks: false);
        var sale = await _returns.TrySellWithNoteAsync(product, 1000, folio, 6_000);
        Assert.Equal(4_000, await BalanceAsync(folio));
        var cashBefore = (await OpenShiftNumbersAsync()).Expected;

        var cancel = await _returns.CancelAsync(sale.Value.SaleId);

        Assert.True(cancel.IsSuccess, cancel.Error?.ToString());
        Assert.Equal(10_000, await BalanceAsync(folio));
        Assert.Equal(cashBefore, (await OpenShiftNumbersAsync()).Expected);
        await using var check = _db.CreateDbContext();
        var refund = await check.SaleReturnRefunds.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((PaymentMethod.CreditNote, RefundStatus.Restored), (refund.Method, refund.Status));
        // SC-005: emitido + restaurado - usado = saldo.
        var movements = await check.CreditNoteMovements.AsNoTracking().ToListAsync(Ct);
        var sum = movements.Where(m => m.Type != CreditNoteMovementType.Redeem).Sum(m => m.AmountCents)
            - movements.Where(m => m.Type == CreditNoteMovementType.Redeem).Sum(m => m.AmountCents);
        Assert.Equal(10_000, sum);
    }

    [Fact]
    public async Task Nota_ElCajeroSoloConsultaElSaldoPorFolio()
    {
        var folio = await IssueNoteAsync(10_000);
        _users.As(_users.Cashier);

        await using var context = _db.CreateDbContext();
        var access = _users.Access(context);
        var handler = new GetCreditNoteBalanceHandler(access, new CreditNoteRepository(context));
        var balance = await handler.HandleAsync(folio, Ct);
        var missing = await handler.HandleAsync("NC-000500", Ct);
        var detail = await new Pos.Application.CreditNotes.GetCreditNoteDetail.GetCreditNoteDetailHandler(access, new CreditNoteRepository(context))
            .HandleAsync(Guid.NewGuid(), Ct);

        Assert.Equal(10_000, balance.Value.BalanceCents);
        Assert.IsType<CreditNoteNotFound>(missing.Error);
        Assert.IsType<Forbidden>(detail.Error);
    }

    /// <summary>Repositorio que devuelve siempre el folio 1 para forzar la violación del índice único.</summary>
    private sealed class DuplicateNumberRepository(IReturnRepository inner) : IReturnRepository
    {
        public Task<long> NextNumberAsync(CancellationToken cancellationToken) => Task.FromResult(1L);

        public void Add(SaleReturn saleReturn) => inner.Add(saleReturn);

        public Task<SaleReturnRefund?> GetRefundAsync(Guid refundId, CancellationToken cancellationToken) =>
            inner.GetRefundAsync(refundId, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, long>> GetReturnedByPaymentAsync(Guid saleId, CancellationToken cancellationToken) =>
            inner.GetReturnedByPaymentAsync(saleId, cancellationToken);

        public Task<ReversalPage> SearchReversalsAsync(ReversalSearch search, CancellationToken cancellationToken) =>
            inner.SearchReversalsAsync(search, cancellationToken);

        public Task<(string ReturnFolio, string SaleFolio, Guid SaleId)?> DescribeRefundAsync(Guid refundId, CancellationToken cancellationToken) =>
            inner.DescribeRefundAsync(refundId, cancellationToken);

        public Task<Pos.Application.Products.SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken) =>
            inner.SaveChangesAsync(cancellationToken);
    }
}
