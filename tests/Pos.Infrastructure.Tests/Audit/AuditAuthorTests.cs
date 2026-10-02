using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>018 (FR-010): una entrada de bitácora nunca queda sin autor.</summary>
public sealed class AuditAuthorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UsuarioActualVacio_LaEntradaQuedaConAutorSistema()
    {
        using var db = await TestDb.CreateAsync();
        db.User.UserId = Guid.Empty;

        Assert.Equal(SystemUser.Id, await SaveAndReadAuthorAsync(db));
    }

    [Fact]
    public async Task UsuarioReal_ConservaElAutor()
    {
        using var db = await TestDb.CreateAsync();
        var user = Guid.CreateVersion7();
        db.User.UserId = user;

        Assert.Equal(user, await SaveAndReadAuthorAsync(db));
    }

    private static async Task<Guid> SaveAndReadAuthorAsync(TestDb db)
    {
        await using (var context = db.CreateDbContext())
        {
            var audit = new AuditLog(context);
            audit.Add(new AuditRecord(AuditActions.DrawerOpened, AuditActions.CashDrawerEntity, Guid.CreateVersion7(), Reason: "Cambio"));
            await audit.SaveAsync(Ct);
        }

        await using var read = db.CreateDbContext();
        return (await read.AuditEntries.AsNoTracking().SingleAsync(Ct)).CreatedBy;
    }
}
