using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>SC-005: una edición con versión desactualizada se rechaza.</summary>
public sealed class ProductConcurrencyTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _productId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        var product = Product.Create("Café", "CAF-001", null, Money.FromCents(8950));
        await using var context = _db.CreateDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync(Ct);
        _productId = product.Id;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SegundoGuardadoConVersionDesactualizada_EsConflictYConservaLosDatosDelPrimero()
    {
        await using var first = _db.CreateDbContext();
        await using var second = _db.CreateDbContext();
        var firstRepository = new ProductRepository(first);
        var secondRepository = new ProductRepository(second);
        var firstCopy = (await firstRepository.GetAsync(_productId, Ct))!;
        var secondCopy = (await secondRepository.GetAsync(_productId, Ct))!;
        var seenVersion = secondCopy.Version;

        firstCopy.Update("Primero", firstCopy.Sku, null, firstCopy.Price, isActive: true);
        Assert.Equal(SaveOutcome.Saved, await firstRepository.SaveChangesAsync(firstCopy, seenVersion, Ct));

        secondCopy.Update("Segundo", secondCopy.Sku, null, secondCopy.Price, isActive: true);
        var outcome = await secondRepository.SaveChangesAsync(secondCopy, seenVersion, Ct);

        Assert.Equal(SaveOutcome.Conflict, outcome);
        await using var check = _db.CreateDbContext();
        var stored = (await new ProductRepository(check).GetAsync(_productId, Ct))!;
        Assert.Equal("Primero", stored.Name);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task VersionEsperadaDistintaDeLaCargada_EsConflict()
    {
        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        var product = (await repository.GetAsync(_productId, Ct))!;
        product.Update("Cambio", product.Sku, null, product.Price, isActive: true);

        // El operador vio la versión 0 (ya no vigente), aunque el contexto cargó la versión 1.
        var outcome = await repository.SaveChangesAsync(product, expectedVersion: 0, Ct);

        Assert.Equal(SaveOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task EdicionConVersionVigente_IncrementaLaVersionYActualizaAuditoria()
    {
        _db.Clock.Advance(TimeSpan.FromHours(1));
        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        var product = (await repository.GetAsync(_productId, Ct))!;
        product.Update("Cambio", product.Sku, null, product.Price, isActive: false);

        Assert.Equal(SaveOutcome.Saved, await repository.SaveChangesAsync(product, 1, Ct));

        await using var check = _db.CreateDbContext();
        var stored = (await new ProductRepository(check).GetAsync(_productId, Ct))!;
        Assert.Equal(2, stored.Version);
        Assert.Equal(_db.Clock.UtcNow, stored.UpdatedAt);
        Assert.NotEqual(stored.CreatedAt, stored.UpdatedAt);
        Assert.False(stored.IsActive);
    }
}
