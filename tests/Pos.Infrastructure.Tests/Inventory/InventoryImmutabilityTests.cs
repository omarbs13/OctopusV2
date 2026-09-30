using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>Criterio de aceptación 5 y FR-010: un movimiento no se modifica ni se borra.</summary>
public sealed class InventoryImmutabilityTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        var product = await InventoryTestSupport.SeedProductAsync(_db, "IMM-1");
        await using var context = _db.CreateDbContext();
        var result = await InventoryTestSupport.Handler(context)
            .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, "3", null, null), Ct);
        Assert.True(result.IsSuccess);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Modifying_a_movement_through_the_context_throws()
    {
        await using var context = _db.CreateDbContext();
        var movement = await context.InventoryMovements.SingleAsync(Ct);
        context.Entry(movement).State = EntityState.Modified;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Deleting_a_movement_through_the_context_throws()
    {
        await using var context = _db.CreateDbContext();
        var movement = await context.InventoryMovements.SingleAsync(Ct);
        context.InventoryMovements.Remove(movement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        Assert.Equal(1, await _db.CreateDbContext().InventoryMovements.CountAsync(Ct));
    }
}
