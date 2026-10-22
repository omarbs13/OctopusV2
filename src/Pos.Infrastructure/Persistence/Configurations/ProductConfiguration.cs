using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Products;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public const string SkuIndexName = "IX_Products_Sku";
    public const string BarcodeIndexName = "IX_Products_Barcode";
    public const string IdentityIndexName = "IX_Products_IdentityKey";

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.NameSearch).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(Product.BarcodeMaxLength);

        // 026: la columna PriceCents conserva su nombre y pasa a ser el precio por pieza (nulable).
        builder.Property(p => p.PiecePriceCents).HasColumnName("PriceCents");
        builder.Ignore(p => p.Price);
        builder.Ignore(p => p.IsTile);
        builder.Ignore(p => p.InitialMode);
        builder.Ignore(p => p.AvailableModes);

        // Campos del proveedor (026). Model se llena en la migración con el nombre anterior.
        builder.Property(p => p.Model).HasMaxLength(Product.NameMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.ModelKey).HasMaxLength(Product.NameMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.Measures).HasMaxLength(Product.MeasuresMaxLength);
        builder.Property(p => p.MeasuresKey).HasMaxLength(Product.MeasuresMaxLength);
        builder.Property(p => p.Brand).HasMaxLength(Product.BrandMaxLength);
        builder.Property(p => p.BrandSearch).HasMaxLength(Product.BrandMaxLength);
        builder.Property(p => p.SupplierId);
        builder.Property(p => p.PackagingThousandths);
        builder.Property(p => p.PiecesPerBox);
        builder.Property(p => p.PricePerSqmCents);
        builder.Property(p => p.PricePerBoxCents);
        builder.Property(p => p.IdentityKey).HasMaxLength(Product.NameMaxLength + Product.MeasuresMaxLength + 40);
        builder.Property(p => p.NeedsReview).IsRequired().HasDefaultValue(false);

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

        // Los productos existentes quedan como "no controla inventario" (FR-023).
        builder.Property(p => p.TracksInventory).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.MinimumStockThousandths).HasColumnName("MinimumStock");
        builder.Ignore(p => p.MinimumStock);

        // Punto de reorden (022): columna nula agregada con AddColumn, sin reconstruir la tabla.
        builder.Property(p => p.ReorderPointThousandths).HasColumnName("ReorderPoint");
        builder.Ignore(p => p.ReorderPoint);

        // Los productos existentes quedan como "no crítico" (009); AddColumn sin reconstruir la tabla.
        builder.Property(p => p.IsCritical).IsRequired().HasDefaultValue(false);

        // Sin relación ni clave foránea (016, research §2): agregarla obligaría a reconstruir Products en
        // SQLite y no impediría apuntar a una categoría borrada (borrado lógico). La integridad la dan los
        // casos de uso en transacciones de escritura serializadas.
        builder.Property(p => p.CategoryId);
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

        // Unicidad de proveedor + modelo + medidas normalizados (026). Los duplicados migrados quedan sin clave.
        builder.HasIndex(p => p.IdentityKey)
            .HasDatabaseName(IdentityIndexName)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL AND \"IdentityKey\" IS NOT NULL");

        builder.HasIndex(p => p.BrandSearch)
            .HasDatabaseName("IX_Products_BrandSearch")
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.MeasuresKey)
            .HasDatabaseName("IX_Products_MeasuresKey")
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.NeedsReview)
            .HasDatabaseName("IX_Products_NeedsReview")
            .HasFilter("\"DeletedAt\" IS NULL AND \"NeedsReview\" = 1");

        builder.HasIndex(p => new { p.TracksInventory, p.IsActive })
            .HasDatabaseName("IX_Products_TracksInventory")
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("IX_Products_CategoryId")
            .HasFilter("\"DeletedAt\" IS NULL");

        // Compuesto para que el orden del listado paginado salga del índice (003, research §2).
        builder.HasIndex(p => new { p.NameSearch, p.Sku })
            .HasDatabaseName("IX_Products_NameSearch")
            .HasFilter("\"DeletedAt\" IS NULL");
    }
}
