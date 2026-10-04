using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>Ventas por cajero (007, FR-020 y FR-026): filtro, nombre del cajero y propiedad de la venta.</summary>
public sealed class SalesByCashierTests : IAsyncLifetime
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
    public async Task Filtro_PorCajero_YNombresDelCajeroEnListadoYDetalle()
    {
        var ana = User.Create("Ana Cajera", "ana", UserRole.Cashier, "hash");
        var beto = User.Create("Beto Cajero", "beto", UserRole.Cashier, "hash");
        await using (var context = _db.CreateDbContext())
        {
            context.Users.AddRange(ana, beto);
            await context.SaveChangesAsync(Ct);
        }

        var product = await SalesTestSupport.SeedProductAsync(_db, "PXC-1");
        await SalesTestSupport.StockAsync(_db, product, "10");
        _db.User.UserId = ana.Id;
        var anaSale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        _db.User.UserId = beto.Id;
        await SalesTestSupport.SellOkAsync(_db, (product, 2000));

        await using var context2 = _db.CreateDbContext();
        var repository = new SaleRepository(context2);
        var all = await repository.SearchAsync(new SaleSearch(null, null, null, null, 1, 100), Ct);
        var onlyAna = await repository.SearchAsync(new SaleSearch(null, null, null, null, 1, 100, ana.Id), Ct);
        var detail = await repository.GetDetailAsync(anaSale.SaleId, Ct);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(["ana"], onlyAna.Items.Select(i => i.CashierName));
        Assert.Equal(["ana", "beto"], all.Items.Select(i => i.CashierName).Order());
        Assert.Equal("ana", detail!.CreatedByName);
        Assert.Equal(ana.Id, detail.CreatedById);
        Assert.Equal(1, await context2.Sales.CountAsync(s => s.CreatedBy == beto.Id, Ct));
    }

    [Fact]
    public async Task UsuarioSinFila_MuestraElIdAbreviado()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "PXC-2");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var ghost = Guid.Parse("abcdef01-0000-7000-8000-000000000000");
        _db.User.UserId = ghost;
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        await using var context = _db.CreateDbContext();
        var detail = await new SaleRepository(context).GetDetailAsync(sale.SaleId, Ct);

        Assert.Equal("abcdef01", detail!.CreatedByName);
    }
}
