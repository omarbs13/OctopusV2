using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CashShifts;
using Pos.Domain.Customers;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Abonos: inmutables salvo la anulación, sin borrado lógico, <c>UpdatedAt/By</c> ni versión; la
/// transacción serializada protege la única transición (plan.md, Complexity Tracking).
/// </summary>
internal sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public const string NumberIndexName = "IX_CustomerPayments_Number";
    public const string RequestIdIndexName = "IX_CustomerPayments_RequestId";

    public void Configure(EntityTypeBuilder<CustomerPayment> builder)
    {
        builder.ToTable("CustomerPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Number).IsRequired();
        builder.Property(p => p.RequestId).IsRequired();
        builder.Property(p => p.AmountCents).IsRequired();
        builder.Property(p => p.Method)
            .HasConversion(method => method.ToCode(), code => PaymentMethodExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(CustomerPayment.ReferenceMaxLength);
        builder.Property(p => p.BalanceBeforeCents).IsRequired();
        builder.Property(p => p.BalanceAfterCents).IsRequired();
        builder.Property(p => p.Status)
            .HasConversion(status => status.ToCode(), code => CustomerPaymentStatusExtensions.FromCode(code))
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(p => p.VoidedAt);
        builder.Property(p => p.VoidedBy);
        builder.Property(p => p.VoidAuthorizedBy);
        builder.Property(p => p.VoidReason).HasMaxLength(CustomerPayment.VoidReasonMaxLength);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Ignore(p => p.Folio);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashShift>()
            .WithMany()
            .HasForeignKey(p => p.CashShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashShift>()
            .WithMany()
            .HasForeignKey(p => p.VoidCashShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Number).HasDatabaseName(NumberIndexName).IsUnique();
        builder.HasIndex(p => p.RequestId).HasDatabaseName(RequestIdIndexName).IsUnique();
        builder.HasIndex(p => new { p.CustomerId, p.CreatedAt }).HasDatabaseName("IX_CustomerPayments_Customer");
        builder.HasIndex(p => p.CashShiftId).HasDatabaseName("IX_CustomerPayments_CashShiftId");
        builder.HasIndex(p => p.VoidCashShiftId).HasDatabaseName("IX_CustomerPayments_VoidCashShiftId");
    }
}
