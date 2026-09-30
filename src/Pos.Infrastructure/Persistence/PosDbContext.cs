using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pos.Domain.Audit;
using Pos.Domain.Business;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Persistence.Configurations;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext : DbContext
{
    /// <summary>Id del aviso de EF "ModelValidationKeyDefaultValueWarning".</summary>
    private const int KeyDefaultValueWarningId = 20600;

    public PosDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    public DbSet<SaleDraft> SaleDrafts => Set<SaleDraft>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();

    public DbSet<User> Users => Set<User>();

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // SaleDrafts.UserId lleva un valor por defecto constante (el id de "Sistema") solo para que la
        // reconstrucción de la tabla en la migración UsersAndRoles conserve la fila anterior (docs/migraciones.md).
        // EF avisa de que una llave no debería tener uno; aquí es intencional.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(new EventId(KeyDefaultValueWarningId, "ModelValidationKeyDefaultValueWarning")));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new UnitOfMeasureConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductImageConfiguration());
        modelBuilder.ApplyConfiguration(new ProductStockConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementConfiguration());
        modelBuilder.ApplyConfiguration(new SaleConfiguration());
        modelBuilder.ApplyConfiguration(new SaleLineConfiguration());
        modelBuilder.ApplyConfiguration(new SalePaymentConfiguration());
        modelBuilder.ApplyConfiguration(new SaleDraftConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new BusinessProfileConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Toda fecha se guarda y se lee como UTC (constitución, Principio IV).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    /// <summary>
    /// Los movimientos de inventario y la bitácora de auditoría son inmutables: no se modifican ni
    /// se borran (004 FR-010, 005 Principio IX).
    /// </summary>
    private void RejectMovementChanges()
    {
        if (ChangeTracker.Entries<InventoryMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los movimientos de inventario no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Las entradas de la bitácora de auditoría no se pueden modificar ni borrar.");
        }
    }
}
