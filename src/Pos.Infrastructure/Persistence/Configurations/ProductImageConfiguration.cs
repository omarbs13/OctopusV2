using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Imágenes de producto dentro de la base (003, research §5): así entran en los respaldos y se
/// guardan en la misma transacción que el producto.
/// </summary>
internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.ProductId);
        builder.Property(i => i.ProductId).ValueGeneratedNever();
        builder.Property(i => i.Content).IsRequired();
        builder.Property(i => i.Thumbnail).IsRequired();
        builder.Property(i => i.Width).IsRequired();
        builder.Property(i => i.Height).IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(ProductImage.ContentTypeMaxLength).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.CreatedBy).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();
        builder.Property(i => i.UpdatedBy).IsRequired();
    }
}
