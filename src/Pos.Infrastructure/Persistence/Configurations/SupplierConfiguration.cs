using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Suppliers;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public const string TaxIdIndexName = "IX_Suppliers_TaxId";

    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        builder.Property(s => s.TaxId).HasMaxLength(Supplier.TaxIdMaxLength);
        builder.Property(s => s.Phone).HasMaxLength(Supplier.PhoneMaxLength);
        builder.Property(s => s.Email).HasMaxLength(Supplier.EmailMaxLength);
        builder.Property(s => s.Address).HasMaxLength(Supplier.AddressMaxLength);
        builder.Property(s => s.PaymentTerms)
            .HasConversion(terms => terms.ToCode(), code => PaymentTermsExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(s => s.CreditDays);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.SearchText).HasMaxLength(Supplier.SearchTextMaxLength).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken().IsRequired();

        // El RUC identifica al proveedor: único también entre inactivos (FR-003, research §9).
        builder.HasIndex(s => s.TaxId)
            .HasDatabaseName(TaxIdIndexName)
            .IsUnique()
            .HasFilter("\"TaxId\" IS NOT NULL");
        builder.HasIndex(s => new { s.IsActive, s.SearchText }).HasDatabaseName("IX_Suppliers_Active_Search");
    }
}
