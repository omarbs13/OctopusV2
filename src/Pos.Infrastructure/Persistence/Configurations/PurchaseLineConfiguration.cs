using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Purchases;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Línea de compra inmutable, sin campos de modificación ni versión (plan.md, Complexity Tracking). El enlace
/// con sus movimientos vive aquí para no tocar <c>InventoryMovements</c> (research §3).
/// </summary>
internal sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("PurchaseLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.LineNumber).IsRequired();
        builder.Property(l => l.ProductName).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(l => l.ProductSku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        builder.Property(l => l.UnitCode).HasMaxLength(UnitOfMeasure.CodeMaxLength).IsRequired();
        builder.Property(l => l.QuantityThousandths).IsRequired();
        builder.Property(l => l.UnitCostCents).IsRequired();
        builder.Property(l => l.AmountCents).IsRequired();
        builder.Ignore(l => l.IsBonus);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(l => l.MovementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(l => l.VoidMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PurchaseId, l.LineNumber })
            .HasDatabaseName("IX_PurchaseLines_Purchase_LineNumber")
            .IsUnique();
        builder.HasIndex(l => new { l.PurchaseId, l.ProductId })
            .HasDatabaseName("IX_PurchaseLines_Purchase_Product")
            .IsUnique();
        builder.HasIndex(l => l.MovementId).HasDatabaseName("IX_PurchaseLines_MovementId").IsUnique();
        builder.HasIndex(l => l.VoidMovementId)
            .HasDatabaseName("IX_PurchaseLines_VoidMovementId")
            .IsUnique()
            .HasFilter("\"VoidMovementId\" IS NOT NULL");
        builder.HasIndex(l => l.ProductId).HasDatabaseName("IX_PurchaseLines_ProductId");
    }
}
