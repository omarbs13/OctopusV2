using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>012, H4: con Turnos sin licencia no se abren turnos; el rechazo lo da el control de acceso.</summary>
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

    [Fact]
    public async Task TurnosSinLicencia_NoAbreTurno()
    {
        var state = new LicenseState(_db.Clock);
        var firstRun = _db.Clock.UtcNow.AddDays(-60);
        state.Set(new LicenseRecord(2, "m", firstRun, firstRun, 30, new HashSet<LicensedModule>()));
        _shifts.License = state;

        Assert.IsType<ModuleNotLicensed>((await _shifts.OpenAsync(0, confirmZero: true)).Error);
    }
}
