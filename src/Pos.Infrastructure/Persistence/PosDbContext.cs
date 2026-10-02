using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Pos.Domain.Audit;
using Pos.Domain.Business;
using Pos.Domain.CashShifts;
using Pos.Domain.Categories;
using Pos.Domain.CreditNotes;
using Pos.Domain.Customers;
using Pos.Domain.Discounts;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Receivables;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Licensing;
using Pos.Infrastructure.Persistence.Configurations;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext : DbContext
{
    /// <summary>Id del aviso de EF "ModelValidationKeyDefaultValueWarning".</summary>
    private const int KeyDefaultValueWarningId = 20600;

    /// <summary>Campos que puede cambiar la anulación de un abono (014, FR-014).</summary>
    private static readonly HashSet<string> PaymentVoidFields =
    [
        nameof(CustomerPayment.Status),
        nameof(CustomerPayment.VoidedAt),
        nameof(CustomerPayment.VoidedBy),
        nameof(CustomerPayment.VoidAuthorizedBy),
        nameof(CustomerPayment.VoidReason),
        nameof(CustomerPayment.VoidCashShiftId),
    ];

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

    public DbSet<CashShift> CashShifts => Set<CashShift>();

    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public DbSet<LicenseSealEntity> LicenseSeals => Set<LicenseSealEntity>();

    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();

    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();

    public DbSet<SaleReturnRefund> SaleReturnRefunds => Set<SaleReturnRefund>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<CreditNoteMovement> CreditNoteMovements => Set<CreditNoteMovement>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Receivable> Receivables => Set<Receivable>();

    public DbSet<ReceivableEntry> ReceivableEntries => Set<ReceivableEntry>();

    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<DiscountApproval> DiscountApprovals => Set<DiscountApproval>();

    public DbSet<SaleDiscount> SaleDiscounts => Set<SaleDiscount>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ShiftCut> ShiftCuts => Set<ShiftCut>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectImmutableChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectImmutableChanges();
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
        modelBuilder.ApplyConfiguration(new CashShiftConfiguration());
        modelBuilder.ApplyConfiguration(new CashMovementConfiguration());
        modelBuilder.ApplyConfiguration(new LicenseSealConfiguration());
        modelBuilder.ApplyConfiguration(new SaleReturnConfiguration());
        modelBuilder.ApplyConfiguration(new SaleReturnLineConfiguration());
        modelBuilder.ApplyConfiguration(new SaleReturnRefundConfiguration());
        modelBuilder.ApplyConfiguration(new CreditNoteConfiguration());
        modelBuilder.ApplyConfiguration(new CreditNoteMovementConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new ReceivableConfiguration());
        modelBuilder.ApplyConfiguration(new ReceivableEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new CouponConfiguration());
        modelBuilder.ApplyConfiguration(new DiscountApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new SaleDiscountConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ShiftCutConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Toda fecha se guarda y se lee como UTC (constitución, Principio IV).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    /// <summary>
    /// Los movimientos de inventario y de efectivo, la bitácora de auditoría, los turnos cerrados, los
    /// cortes de caja y el libro de las cuentas por cobrar son inmutables: no se modifican ni se borran (004 FR-010,
    /// 005 Principio IX, 008 research §10, 014 FR-014, 017 FR-013).
    /// </summary>
    private void RejectImmutableChanges()
    {
        if (ChangeTracker.Entries<InventoryMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los movimientos de inventario no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<CashMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los movimientos de efectivo no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<CashShift>().Any(e =>
                e.State is EntityState.Modified or EntityState.Deleted
                && e.Property(nameof(CashShift.Status)).OriginalValue is CashShiftStatus.Closed))
        {
            throw new InvalidOperationException("Un turno cerrado no se puede modificar ni borrar.");
        }

        // 017, FR-013: los Cortes X y Z no se deshacen, modifican ni borran.
        if (ChangeTracker.Entries<ShiftCut>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los cortes de caja no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<SaleReturn>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<SaleReturnLine>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Las cancelaciones y devoluciones no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<CreditNote>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<CreditNoteMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Las notas de crédito y sus movimientos no se pueden modificar ni borrar.");
        }

        // Única mutación permitida: PENDING_REVERSAL -> REVERSED, una sola vez (FR-018).
        if (ChangeTracker.Entries<SaleReturnRefund>().Any(e =>
                e.State is EntityState.Deleted
                || (e.State is EntityState.Modified
                    && !(e.Property(nameof(SaleReturnRefund.Status)).OriginalValue is RefundStatus.PendingReversal
                        && e.Property(nameof(SaleReturnRefund.Status)).CurrentValue is RefundStatus.Reversed))))
        {
            throw new InvalidOperationException("Un reintegro solo puede pasar de pendiente de reversa a reversado.");
        }

        // 015: los descuentos de una venta registrada y las aprobaciones de descuento son inmutables.
        if (ChangeTracker.Entries<SaleDiscount>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<DiscountApproval>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los descuentos de una venta y sus aprobaciones no se pueden modificar ni borrar.");
        }

        // 014: el libro de las cuentas por cobrar es inmutable y una cuenta nunca se borra (se cancela).
        if (ChangeTracker.Entries<ReceivableEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Los movimientos de las cuentas por cobrar no se pueden modificar ni borrar.");
        }

        if (ChangeTracker.Entries<Receivable>().Any(e => e.State is EntityState.Deleted))
        {
            throw new InvalidOperationException("Una cuenta por cobrar no se puede borrar.");
        }

        // Única mutación de un abono: ACTIVE -> VOIDED, una sola vez, solo con los campos de la anulación (FR-014).
        if (ChangeTracker.Entries<CustomerPayment>().Any(e =>
                e.State is EntityState.Deleted
                || (e.State is EntityState.Modified && !IsPaymentVoid(e))))
        {
            throw new InvalidOperationException("Un abono solo puede pasar de vigente a anulado.");
        }

        if (ChangeTracker.Entries<AuditEntry>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<AuditFieldChange>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Las entradas de la bitácora de auditoría no se pueden modificar ni borrar.");
        }
    }

    private static bool IsPaymentVoid(EntityEntry<CustomerPayment> entry) =>
        entry.Property(nameof(CustomerPayment.Status)).OriginalValue is CustomerPaymentStatus.Active
        && entry.Property(nameof(CustomerPayment.Status)).CurrentValue is CustomerPaymentStatus.Voided
        && entry.Properties.Where(p => p.IsModified).All(p => PaymentVoidFields.Contains(p.Metadata.Name));
}
