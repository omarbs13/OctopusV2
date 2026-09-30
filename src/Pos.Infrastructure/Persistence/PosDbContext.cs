using Microsoft.EntityFrameworkCore;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence.Configurations;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectMovementChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectMovementChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new UnitOfMeasureConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductImageConfiguration());
        modelBuilder.ApplyConfiguration(new ProductStockConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Toda fecha se guarda y se lee como UTC (constitución, Principio IV).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    /// <summary>Los movimientos de inventario son inmutables: no se modifican ni se borran (FR-010).</summary>
    private void RejectMovementChanges()
    {
        if (ChangeTracker.Entries<InventoryMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los movimientos de inventario no se pueden modificar ni borrar.");
        }
    }
}
