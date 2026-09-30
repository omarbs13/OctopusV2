using Microsoft.EntityFrameworkCore;
using Pos.Domain.Audit;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>La apertura del cajón sin venta queda en la bitácora con SQLite real (006, Principio IX).</summary>
public sealed class CashDrawerAuditTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AddYSaveAsync_PersistenUnaEntradaDrawerOpenedConUsuarioYFecha()
    {
        var id = Guid.CreateVersion7();
        await using (var context = _db.CreateDbContext())
        {
            var log = new AuditLog(context);
            log.Add("DRAWER_OPENED", "CashDrawer", id, "Motivo: Cambio; Resultado: OK");
            await log.SaveAsync(Ct);
        }

        await using var read = _db.CreateDbContext();
        var entry = await read.AuditEntries.SingleAsync(e => e.EntityId == id, Ct);
        Assert.Equal("DRAWER_OPENED", entry.Action);
        Assert.Equal("CashDrawer", entry.EntityType);
        Assert.Equal("Motivo: Cambio; Resultado: OK", entry.Details);
        Assert.Equal(_db.User.UserId, entry.CreatedBy);
        Assert.Equal(_db.Clock.UtcNow, entry.CreatedAt);
    }

    [Fact]
    public async Task DetalleLargo_SeRecortaA500Caracteres()
    {
        var id = Guid.CreateVersion7();
        await using (var context = _db.CreateDbContext())
        {
            var log = new AuditLog(context);
            log.Add("DRAWER_OPENED", "CashDrawer", id, new string('x', 900));
            await log.SaveAsync(Ct);
        }

        await using var read = _db.CreateDbContext();
        var entry = await read.AuditEntries.SingleAsync(e => e.EntityId == id, Ct);
        Assert.Equal(AuditEntry.DetailsMaxLength, entry.Details!.Length);
    }
}
