using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>011, H3 y FR-010: con la licencia vencida no se abren turnos, pero uno abierto se puede cerrar.</summary>
public sealed class OpenShiftLicenseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _shifts = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task Vencida_NoAbreTurno_PeroPermiteCerrarElQueEstabaAbierto()
    {
        var state = new LicenseState(_db.Clock);
        var firstRun = _db.Clock.UtcNow.AddDays(-3);
        state.Set(new LicenseRecord(1, "m", firstRun, firstRun, null));
        _shifts.License = state;
        var shift = (await _shifts.OpenAsync(0, confirmZero: true)).Value;

        state.Set(new LicenseRecord(1, "m", firstRun.AddDays(-60), firstRun.AddDays(-60), null));

        Assert.IsType<LicenseExpired>((await _shifts.OpenAsync(0, confirmZero: true)).Error);
        var count = await _shifts.CountAsync(shift.ShiftId, 0);
        Assert.True((await _shifts.CloseAsync(shift.ShiftId, count.Value.Version, 0, count.Value.ExpectedCents)).IsSuccess);
    }
}
