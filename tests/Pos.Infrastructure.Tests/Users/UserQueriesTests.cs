using Pos.Application.Users;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.Users;

/// <summary>Consultas de usuarios con SQLite real: búsqueda sin acentos, "Sistema" oculto y cajeros con ventas.</summary>
public sealed class UserQueriesTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<User> AddUserAsync(string fullName, string userName, UserRole role = UserRole.Cashier, bool active = true)
    {
        var user = User.Create(fullName, userName, role, "hash");
        if (!active)
        {
            user.Deactivate();
        }

        await using var context = _db.CreateDbContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user;
    }

    [Fact]
    public async Task Busqueda_SinAcentosNiMayusculas_NuncaIncluyeASistemaYRespetaElFiltroDeInactivos()
    {
        await AddUserAsync("María López", "maria");
        await AddUserAsync("Pedro Gómez", "pedro", active: false);

        await using var context = _db.CreateDbContext();
        var repository = new UserRepository(context);

        var maria = await repository.SearchAsync(new UserSearch("maria", IncludeInactive: false, 1, UserPage.DefaultPageSize), Ct);
        var activos = await repository.SearchAsync(new UserSearch(null, IncludeInactive: false, 1, UserPage.DefaultPageSize), Ct);
        var todos = await repository.SearchAsync(new UserSearch(null, IncludeInactive: true, 1, UserPage.DefaultPageSize), Ct);

        Assert.Equal(["María López"], maria.Items.Select(u => u.FullName));
        Assert.Equal(["María López"], activos.Items.Select(u => u.FullName));
        Assert.Equal(["María López", "Pedro Gómez"], todos.Items.Select(u => u.FullName));
        Assert.DoesNotContain(todos.Items, u => u.UserName == "Sistema");
    }

    [Fact]
    public async Task Cajeros_SonLosActivosMasLosInactivosConVentas_SinSistema()
    {
        var active = await AddUserAsync("Ana Activa", "ana");
        var sold = await AddUserAsync("Ivan Inactivo con ventas", "ivan", active: false);
        await AddUserAsync("Nora Inactiva sin ventas", "nora", active: false);

        var product = await SalesTestSupport.SeedProductAsync(_db, "CAJ-1");
        await SalesTestSupport.StockAsync(_db, product, "10");
        _db.User.UserId = sold.Id;
        await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        await using var context = _db.CreateDbContext();
        var cashiers = await new UserRepository(context).ListCashiersAsync(Ct);

        Assert.Equal([active.Id, sold.Id], cashiers.Select(c => c.Id).Order());
        Assert.DoesNotContain(cashiers, c => c.FullName == "Sistema");
    }

    [Fact]
    public async Task ElAdministradorActivo_SeCuentaSinSistemaNiInactivos()
    {
        await AddUserAsync("Admin A", "adminA", UserRole.Admin);
        await AddUserAsync("Admin B", "adminB", UserRole.Admin, active: false);
        await AddUserAsync("Caja", "caja");

        await using var context = _db.CreateDbContext();
        var repository = new UserRepository(context);

        Assert.Equal(1, await repository.CountActiveAdminsAsync(Ct));
        Assert.True(await repository.AnyRealUserAsync(Ct));
    }
}
