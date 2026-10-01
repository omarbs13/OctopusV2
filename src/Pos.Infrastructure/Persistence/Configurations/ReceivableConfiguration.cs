using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Customers;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Cuenta por cobrar: nunca se borra, se cancela; por eso no tiene <c>DeletedAt</c> (plan.md, Complexity Tracking).</summary>
internal sealed class ReceivableConfiguration : IEntityTypeConfiguration<Receivable>
{
    public void Configure(EntityTypeBuilder<Receivable> builder)
    {
        builder.ToTable("Receivables");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.CustomerName).HasMaxLength(Receivable.CustomerNameMaxLength).IsRequired();
        builder.Property(r => r.OriginalCents).IsRequired();
        builder.Property(r => r.BalanceCents).IsRequired();
        builder.Property(r => r.Status)
            .HasConversion(status => status.ToCode(), code => ReceivableStatusExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(r => r.OverLimitAuthorizedBy);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.Property(r => r.UpdatedBy).IsRequired();
        builder.Property(r => r.Version).IsConcurrencyToken().IsRequired();

        // Las llaves foráneas van solo de las tablas nuevas a las existentes: no se reconstruye Sales (research §14).
        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(r => r.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Entries)
            .WithOne()
            .HasForeignKey(e => e.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.SaleId).HasDatabaseName("IX_Receivables_SaleId").IsUnique();

        // Orden FIFO, saldo del cliente y reporte (research §3).
        builder.HasIndex(r => new { r.CustomerId, r.Status, r.CreatedAt, r.Id }).HasDatabaseName("IX_Receivables_Customer_Status");
    }
}
