using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public const string SkuIndexName = "IX_Products_Sku";
    public const string BarcodeIndexName = "IX_Products_Barcode";

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.NameSearch).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(Product.BarcodeMaxLength);

        builder.Property(p => p.Price)
            .HasColumnName("PriceCents")
            .HasConversion(money => money.Cents, cents => Money.FromCents(cents))
            .IsRequired();

        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.UpdatedBy).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken().IsRequired();

        builder.Ignore(p => p.IsDeleted);

        // Unicidad solo entre productos no borrados: permite reutilizar SKU y código de barras.
        builder.HasIndex(p => p.Sku)
            .HasDatabaseName(SkuIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.Barcode)
            .HasDatabaseName(BarcodeIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL AND \"Barcode\" IS NOT NULL");

        builder.HasIndex(p => p.NameSearch)
            .HasDatabaseName("IX_Products_NameSearch")
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
