using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Movimientos inmutables: solo llevan fecha y usuario de creación, sin borrado lógico ni versión
/// (desviación justificada en plan.md, Complexity Tracking).
/// </summary>
internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.Type)
            .HasConversion(t => t.ToCode(), code => MovementTypeExtensions.FromCode(code))
            .HasMaxLength(12)
            .IsRequired();
        builder.Property(m => m.QuantityThousandths).HasColumnName("Quantity").IsRequired();
        builder.Property(m => m.ResultingStockThousandths).HasColumnName("ResultingStock").IsRequired();
        builder.Ignore(m => m.Quantity);
        builder.Ignore(m => m.ResultingStock);
        builder.Property(m => m.Reason).HasMaxLength(InventoryMovement.ReasonMaxLength);
        builder.Property(m => m.Reference).HasMaxLength(InventoryMovement.ReferenceMaxLength);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).IsRequired();

        builder.HasIndex(m => new { m.ProductId, m.Sequence })
            .HasDatabaseName("IX_InventoryMovements_Product_Sequence")
            .IsUnique();
        builder.HasIndex(m => new { m.CreatedAt, m.Id })
            .HasDatabaseName("IX_InventoryMovements_CreatedAt");
        builder.HasIndex(m => new { m.Type, m.CreatedAt })
            .HasDatabaseName("IX_InventoryMovements_Type_CreatedAt");
    }
}
