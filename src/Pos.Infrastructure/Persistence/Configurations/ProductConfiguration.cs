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

        // El valor por defecto solo sirve para que la migración asigne "Pieza" a los productos
        // anteriores a 003 (clarificación 1); el dominio siempre asigna la unidad.
        builder.Property(p => p.UnitCode)
            .HasMaxLength(UnitOfMeasure.CodeMaxLength)
            .IsRequired()
            .HasDefaultValue(UnitOfMeasure.Default.Code);
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(p => p.UnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.UpdatedBy).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken().IsRequired();

        builder.Ignore(p => p.IsDeleted);
        builder.Ignore(p => p.ImageChanged);

        // 0 o 1 imagen por producto; la fila se borra al quitar la imagen (003, FR-030).
        builder.HasOne(p => p.Image)
            .WithOne()
            .HasForeignKey<ProductImage>(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Image).AutoInclude(false);

        // Unicidad solo entre productos no borrados: permite reutilizar SKU y código de barras.
        builder.HasIndex(p => p.Sku)
            .HasDatabaseName(SkuIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.Barcode)
            .HasDatabaseName(BarcodeIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL AND \"Barcode\" IS NOT NULL");

        // Compuesto para que el orden del listado paginado salga del índice (003, research §2).
        builder.HasIndex(p => new { p.NameSearch, p.Sku })
            .HasDatabaseName("IX_Products_NameSearch")
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
