using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
{
    public void Configure(EntityTypeBuilder<ProductStock> builder)
    {
        builder.ToTable("ProductStocks");
        builder.HasKey(s => s.ProductId);
        builder.Property(s => s.ProductId).ValueGeneratedNever();
        builder.HasOne<Product>()
            .WithOne()
            .HasForeignKey<ProductStock>(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.OnHandThousandths).HasColumnName("OnHand").IsRequired();
        builder.Ignore(s => s.OnHand);
        builder.Property(s => s.MovementCount).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken().IsRequired();
    }
}
