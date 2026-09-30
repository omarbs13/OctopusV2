using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CashShifts;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Movimientos de efectivo inmutables: solo llevan fecha y usuario de creación, sin borrado lógico
/// ni versión, igual que <c>InventoryMovement</c> (plan.md, Constitution Check).
/// </summary>
internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.Type)
            .HasConversion(t => t.ToCode(), code => CashMovementTypeExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(m => m.AmountCents).IsRequired();
        builder.Property(m => m.Reason).HasMaxLength(CashMovement.ReasonMaxLength).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).IsRequired();

        builder.HasIndex(m => new { m.CashShiftId, m.Sequence })
            .HasDatabaseName("IX_CashMovements_Shift_Sequence")
            .IsUnique();
    }
}
