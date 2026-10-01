using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cancelaciones y devoluciones inmutables: solo fecha y usuario de creación, sin borrado lógico ni
/// versión, igual que <c>InventoryMovement</c> (plan.md, Complexity Tracking).
/// </summary>
internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public const string NumberIndexName = "IX_SaleReturns_Number";

    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("SaleReturns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Number).IsRequired();
        builder.Property(r => r.Kind)
            .HasConversion(kind => kind.ToCode(), code => ReturnKindExtensions.FromCode(code))
            .HasMaxLength(12)
            .IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(SaleReturn.ReasonMaxLength).IsRequired();
        builder.Property(r => r.AuthorizedBy).IsRequired();
        builder.Property(r => r.TotalCents).IsRequired();
        builder.Property(r => r.Compensation)
            .HasConversion(c => c.ToCode(), code => ReturnCompensationExtensions.FromCode(code))
            .HasMaxLength(12)
            .IsRequired();
        // Sin llave foránea hacia CashShifts ni CreditNotes (008, research §5; la nota apunta a la devolución).
        builder.Property(r => r.CashShiftId);
        builder.Property(r => r.CreditNoteId);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();
        builder.Ignore(r => r.Folio);

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(r => r.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.SaleReturnId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Refunds)
            .WithOne()
            .HasForeignKey(f => f.SaleReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.Number).HasDatabaseName(NumberIndexName).IsUnique();
        builder.HasIndex(r => r.SaleId).HasDatabaseName("IX_SaleReturns_SaleId");
        builder.HasIndex(r => r.CashShiftId).HasDatabaseName("IX_SaleReturns_CashShiftId");
    }
}
