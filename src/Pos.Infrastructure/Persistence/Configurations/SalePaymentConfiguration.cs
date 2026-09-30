using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Method)
            .HasConversion(method => method.ToCode(), code => PaymentMethodExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(p => p.AmountCents).IsRequired();
        builder.Property(p => p.ReceivedCents);
        builder.Property(p => p.ChangeCents);
        builder.Property(p => p.Reference).HasMaxLength(SalePayment.ReferenceMaxLength);
    }
}
