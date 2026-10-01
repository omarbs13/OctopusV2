using Microsoft.EntityFrameworkCore;
using Pos.Application.Licensing;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>012, FR-018: la copia protegida de la fecha de inicio vive en la base y no se puede editar.</summary>
public sealed class LicenseSealStoreTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private LicenseSealStore Store(string machineId = "maquina-a") => new(_db, new FixedMachine(machineId));

    [Fact]
    public async Task SinFila_DevuelveNulo() =>
        Assert.Null(await Store().ReadAsync(Ct));

    [Fact]
    public async Task Guardar_YLeer_ConservaLasFechas_YUnaSegundaEscrituraActualizaLaMismaFila()
    {
        var store = Store();
        await store.WriteAsync(new LicenseSeal(Start, Start), Ct);
        await store.WriteAsync(new LicenseSeal(Start, Start.AddDays(2)), Ct);

        var seal = await store.ReadAsync(Ct);

        Assert.Equal(new LicenseSeal(Start, Start.AddDays(2)), seal);
        await using var context = _db.CreateDbContext();
        Assert.Equal(1, await context.LicenseSeals.CountAsync(Ct));
    }

    [Fact]
    public async Task CargaAlterada_EquivaleAAusente()
    {
        await Store().WriteAsync(new LicenseSeal(Start, Start), Ct);
        await using (var context = _db.CreateDbContext())
        {
            var row = await context.LicenseSeals.SingleAsync(Ct);
            var tampered = (byte[])row.Payload.Clone();
            tampered[^1] ^= 0xFF;
            row.Payload = tampered;
            await context.SaveChangesAsync(Ct);
        }

        Assert.Null(await Store().ReadAsync(Ct));
    }

    [Fact]
    public async Task CopiaDeOtraMaquina_EquivaleAAusente()
    {
        await Store("maquina-a").WriteAsync(new LicenseSeal(Start, Start), Ct);

        Assert.Null(await Store("maquina-b").ReadAsync(Ct));
    }

    private sealed class FixedMachine(string id) : IMachineIdProvider
    {
        public string GetMachineId() => id;
    }
}
