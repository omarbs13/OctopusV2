using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;
using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> builder)
    {
        builder.ToTable("SaleReturnLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.QuantityThousandths).IsRequired();
        builder.Property(l => l.AmountCents).IsRequired();

        builder.HasOne<SaleLine>()
            .WithMany()
            .HasForeignKey(l => l.SaleLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(l => l.ReturnMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SaleReturnId, l.SaleLineId })
            .HasDatabaseName("IX_SaleReturnLines_Return_SaleLine")
            .IsUnique();
        builder.HasIndex(l => l.SaleLineId).HasDatabaseName("IX_SaleReturnLines_SaleLine");
    }
}
