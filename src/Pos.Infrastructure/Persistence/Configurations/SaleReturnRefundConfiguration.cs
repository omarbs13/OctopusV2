using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleReturnRefundConfiguration : IEntityTypeConfiguration<SaleReturnRefund>
{
    public void Configure(EntityTypeBuilder<SaleReturnRefund> builder)
    {
        builder.ToTable("SaleReturnRefunds");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Method)
            .HasConversion(method => method.ToCode(), code => PaymentMethodExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(f => f.AmountCents).IsRequired();
        builder.Property(f => f.Status)
            .HasConversion(status => status.ToCode(), code => RefundStatusExtensions.FromCode(code))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(f => f.ReversedAt);
        builder.Property(f => f.ReversedBy);

        builder.HasOne<SalePayment>()
            .WithMany()
            .HasForeignKey(f => f.SalePaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lista del Administrador (research, plan): solo los pendientes de reversa manual.
        builder.HasIndex(f => new { f.Status, f.Id })
            .HasDatabaseName("IX_SaleReturnRefunds_Pending")
            .HasFilter("\"Status\" = 'PENDING_REVERSAL'");
        builder.HasIndex(f => f.SalePaymentId).HasDatabaseName("IX_SaleReturnRefunds_SalePayment");
    }
}
