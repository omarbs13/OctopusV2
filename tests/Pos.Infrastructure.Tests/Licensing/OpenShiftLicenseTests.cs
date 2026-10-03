using Pos.Application.Abstractions;
using Pos.Application.CashShifts.SearchShiftCuts;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 012, H4; 025, H6 escenarios 7 y 8 (FR-030a, Principio I): sin Turnos y arqueo no se abren turnos, pero el turno
/// que ya estaba abierto se consulta, cuenta y cierra aunque el módulo haya vencido o el sistema esté bloqueado.
/// En bloqueo no se abren turnos, no hay movimientos de efectivo ni consultas de turnos y cortes anteriores.
/// </summary>
public sealed class OpenShiftLicenseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _shifts = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _shifts = await ShiftTestSupport.CreateAsync(_db);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> OpenShiftWithCashShiftsAsync()
    {
        _shifts.License = TestLicenses.Licensed(_db.Clock, LicensedModule.CashShifts);
        var opened = await _shifts.OpenAsync();
        Assert.True(opened.IsSuccess, opened.Error?.ToString());
        return opened.Value.ShiftId;
    }

    private async Task CountAndCloseAsync(Guid shiftId)
    {
        var current = await _shifts.CurrentAsync();
        Assert.True(current.IsSuccess, current.Error?.ToString());
        Assert.Equal(shiftId, current.Value!.ShiftId);

        var count = await _shifts.CountAsync(shiftId, 50_000);
        Assert.True(count.IsSuccess, count.Error?.ToString());

        var closed = await _shifts.CloseAsync(shiftId, count.Value.Version, 50_000, count.Value.ExpectedCents);
        Assert.True(closed.IsSuccess, closed.Error?.ToString());
    }

    [Fact]
    public async Task TurnosSinLicencia_NoAbreTurno()
    {
        _shifts.License = TestLicenses.Licensed(_db.Clock);

        Assert.IsType<ModuleNotLicensed>((await _shifts.OpenAsync(0, confirmZero: true)).Error);
    }

    [Fact]
    public async Task EnBloqueo_ElTurnoAbiertoSeCuentaYCierra_PeroNoSeAbreOtroNiHayMovimientosNiConsultasAnteriores()
    {
        var shiftId = await OpenShiftWithCashShiftsAsync();
        _shifts.License = TestLicenses.Exactly(_db.Clock);
        Assert.True(_shifts.License.Current.IsBlocked);

        Assert.IsType<SystemNotActivated>((await _shifts.MoveAsync(shiftId, Pos.Domain.CashShifts.CashMovementType.Out, 1_000)).Error);
        await CountAndCloseAsync(shiftId);

        Assert.IsType<SystemNotActivated>((await _shifts.OpenAsync()).Error);
        Assert.IsType<SystemNotActivated>((await _shifts.SearchShiftsAsync()).Error);
        Assert.IsType<SystemNotActivated>((await _shifts.SearchCutsAsync(new SearchShiftCutsQuery(null, null, null, null))).Error);
    }

    [Fact]
    public async Task SinBloqueo_ConTurnosVencido_ElTurnoAbiertoSeCuentaYCierra()
    {
        var shiftId = await OpenShiftWithCashShiftsAsync();
        _shifts.License = TestLicenses.Licensed(_db.Clock);
        Assert.False(_shifts.License.IsModuleActive(LicensedModule.CashShifts));

        await CountAndCloseAsync(shiftId);
    }
}
