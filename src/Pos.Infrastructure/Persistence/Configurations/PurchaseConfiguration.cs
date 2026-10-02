using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Purchases;
using Pos.Domain.Suppliers;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Compra inmutable salvo la anulación; sin borrado lógico (plan.md, Complexity Tracking).</summary>
internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public const string InvoiceIndexName = "IX_Purchases_Supplier_InvoiceKey";

    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.SupplierName).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        builder.Property(p => p.InvoiceNumber).HasMaxLength(Purchase.InvoiceNumberMaxLength).IsRequired();
        builder.Property(p => p.InvoiceKey).HasMaxLength(Purchase.InvoiceNumberMaxLength).IsRequired();
        builder.Property(p => p.InvoiceDate).IsRequired();
        builder.Property(p => p.LineCount).IsRequired();
        builder.Property(p => p.SubtotalCents).IsRequired();
        builder.Property(p => p.TaxCents).IsRequired();
        builder.Property(p => p.TotalCents).IsRequired();
        builder.Property(p => p.Status)
            .HasConversion(status => status.ToCode(), code => PurchaseStatusExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(p => p.VoidedAt);
        builder.Property(p => p.VoidedBy);
        builder.Property(p => p.VoidReason).HasMaxLength(Purchase.VoidReasonMaxLength);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.UpdatedBy).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken().IsRequired();

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Una factura por proveedor entre las compras vigentes: al anular queda libre (FR-011, research §5).
        builder.HasIndex(p => new { p.SupplierId, p.InvoiceKey })
            .HasDatabaseName(InvoiceIndexName)
            .IsUnique()
            .HasFilter("\"Status\" = 'ACTIVE'");
        builder.HasIndex(p => new { p.InvoiceDate, p.CreatedAt, p.Id }).HasDatabaseName("IX_Purchases_InvoiceDate");
        builder.HasIndex(p => new { p.SupplierId, p.InvoiceDate }).HasDatabaseName("IX_Purchases_Supplier_InvoiceDate");
    }
}
