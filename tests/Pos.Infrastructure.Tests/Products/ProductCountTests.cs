using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

public sealed class ProductCountTests : IAsyncLifetime
{
    private TestDb _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CountActive_CuentaSoloActivosNoBorrados()
    {
        var inactive = Product.Create("Inactivo", "I-1", null, Money.FromCents(100), "H87");
        inactive.Update(inactive.Name, inactive.Sku, null, inactive.Price, "H87", isActive: false);
        var deleted = Product.Create("Borrado", "B-1", null, Money.FromCents(100), "H87");
        deleted.Delete(_db.Clock.UtcNow);
        await using (var context = _db.CreateDbContext())
        {
            context.Products.AddRange(
                Product.Create("Activo 1", "A-1", null, Money.FromCents(100), "H87"),
                Product.Create("Activo 2", "A-2", null, Money.FromCents(100), "H87"),
                inactive,
                deleted);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = _db.CreateDbContext();
        Assert.Equal(2, await new ProductRepository(check).CountActiveAsync(TestContext.Current.CancellationToken));
    }
}
