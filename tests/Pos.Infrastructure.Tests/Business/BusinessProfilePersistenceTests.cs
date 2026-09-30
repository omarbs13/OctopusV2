using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Domain.Business;
using Pos.Infrastructure.Business;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Business;

/// <summary>Datos del negocio con SQLite real (006, US1).</summary>
public sealed class BusinessProfilePersistenceTests : IAsyncLifetime
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
    public async Task GuardaYReleeElPerfilConLogotipo()
    {
        await using (var context = _db.CreateDbContext())
        {
            var repository = new BusinessProfileRepository(context);
            var profile = BusinessProfile.Create("Mi Tienda", "Calle 1", "555", "xaxx010101000", "Gracias\nVuelva pronto");
            profile.SetLogo([1, 2, 3, 4]);
            repository.Add(profile);
            Assert.Equal(SaveStatus.Saved, (await repository.SaveChangesAsync(Ct)).Status);
        }

        await using var read = _db.CreateDbContext();
        var loaded = await new BusinessProfileRepository(read).GetAsync(Ct);

        Assert.NotNull(loaded);
        Assert.Equal("Mi Tienda", loaded.TradeName);
        Assert.Equal("XAXX010101000", loaded.TaxId);
        Assert.Equal("Gracias\nVuelva pronto", loaded.FooterMessage);
        Assert.Equal([1, 2, 3, 4], loaded.Logo);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task SinFila_DevuelveNulo()
    {
        await using var context = _db.CreateDbContext();

        Assert.Null(await new BusinessProfileRepository(context).GetAsync(Ct));
    }

    [Fact]
    public async Task SegundaEscritura_ActualizaLaMismaFila()
    {
        await using (var context = _db.CreateDbContext())
        {
            var repository = new BusinessProfileRepository(context);
            repository.Add(BusinessProfile.Create("Uno", "Calle 1", "555", null, null));
            await repository.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateDbContext())
        {
            var repository = new BusinessProfileRepository(context);
            var profile = (await repository.GetAsync(Ct))!;
            profile.Update("Dos", "Calle 2", "666", null, null);
            Assert.Equal(SaveStatus.Saved, (await repository.SaveChangesAsync(Ct)).Status);
        }

        await using var read = _db.CreateDbContext();
        var rows = await read.BusinessProfiles.ToListAsync(Ct);
        var row = Assert.Single(rows);
        Assert.Equal("Dos", row.TradeName);
        Assert.Equal(2, row.Version);
    }
}
