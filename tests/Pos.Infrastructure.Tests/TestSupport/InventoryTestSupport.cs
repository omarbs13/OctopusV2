using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Users.Session;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;

namespace Pos.Infrastructure.Tests.TestSupport;

public static class InventoryTestSupport
{
    /// <summary>Manejador real sobre un contexto, como lo arma la composición (un ámbito por operación).</summary>
    public static RegisterMovementHandler Handler(PosDbContext context, IInventoryRepository? inventory = null) =>
        new(
            new AllowAllAccessControl(),
            new UserSession(),
            new ProductRepository(context),
            inventory ?? new InventoryRepository(context),
            new WriteTransactions(context),
            new RegisterMovementValidator(),
            NullLogger<RegisterMovementHandler>.Instance);

    public static async Task<Product> SeedProductAsync(
        IDbContextFactory<PosDbContext> factory,
        string sku,
        string unit = "H87",
        bool tracks = true,
        long? minimumThousandths = null,
        bool active = true)
    {
        var product = Product.Create(
            $"Producto {sku}",
            sku,
            null,
            Money.FromCents(1000),
            unit,
            tracks,
            minimumThousandths is { } m ? Quantity.FromThousandths(m) : null);
        if (!active)
        {
            product.Update(product.Name, product.Sku, null, product.Price, unit, isActive: false, tracks, product.MinimumStock);
        }

        await using var context = factory.CreateDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }
}
