using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.ToTable("SaleLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Position).IsRequired();
        builder.Property(l => l.ProductName).HasMaxLength(SaleLine.NameMaxLength).IsRequired();
        builder.Property(l => l.ProductSku).HasMaxLength(SaleLine.SkuMaxLength).IsRequired();
        builder.Property(l => l.UnitCode).HasMaxLength(UnitOfMeasure.CodeMaxLength).IsRequired();
        builder.Property(l => l.DecimalPlaces).IsRequired();
        builder.Property(l => l.UnitPriceCents).IsRequired();
        builder.Property(l => l.QuantityThousandths).IsRequired();
        builder.Property(l => l.AmountCents).IsRequired();
        builder.Property(l => l.ReturnedQuantity).IsRequired().HasDefaultValue(0L);
        builder.Ignore(l => l.UnitPrice);
        builder.Ignore(l => l.Quantity);
        builder.Ignore(l => l.Amount);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(l => l.SaleMovementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(l => l.CancellationMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SaleId, l.Position })
            .HasDatabaseName("IX_SaleLines_Sale_Position")
            .IsUnique();
        builder.HasIndex(l => l.ProductId).HasDatabaseName("IX_SaleLines_Product");
    }
}
