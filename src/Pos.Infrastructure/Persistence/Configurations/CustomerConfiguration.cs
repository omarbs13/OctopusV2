using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Customers;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public const string TaxIdIndexName = "IX_Customers_TaxId";

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(Customer.EmailMaxLength);
        builder.Property(c => c.TaxId).HasMaxLength(Customer.TaxIdMaxLength);
        builder.Property(c => c.CreditLimitCents).IsRequired();
        builder.Property(c => c.CreditMode)
            .HasConversion(mode => mode.ToCode(), code => CreditModeExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.SearchText).HasMaxLength(Customer.SearchTextMaxLength).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.UpdatedBy).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken().IsRequired();
        builder.Ignore(c => c.CanBuyOnCredit);

        // El RUC identifica a la persona: único también entre clientes inactivos (research §10).
        builder.HasIndex(c => c.TaxId)
            .HasDatabaseName(TaxIdIndexName)
            .IsUnique()
            .HasFilter("\"TaxId\" IS NOT NULL");
        builder.HasIndex(c => new { c.IsActive, c.SearchText }).HasDatabaseName("IX_Customers_Active_Search");
    }
}
