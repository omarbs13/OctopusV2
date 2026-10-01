using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CashShifts;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CashShiftConfiguration : IEntityTypeConfiguration<CashShift>
{
    public const string NumberIndexName = "IX_CashShifts_Number";
    public const string OpenPerRegisterIndexName = "IX_CashShifts_OpenPerRegister";

    public void Configure(EntityTypeBuilder<CashShift> builder)
    {
        builder.ToTable("CashShifts");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Number).IsRequired();
        builder.Property(s => s.RegisterCode).HasMaxLength(CashShift.RegisterCodeMaxLength).IsRequired();
        builder.Property(s => s.Status)
            .HasConversion(status => status.ToCode(), code => CashShiftStatusExtensions.FromCode(code))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(s => s.OpenedBy).IsRequired();
        builder.Property(s => s.OpenedAt).IsRequired();
        builder.Property(s => s.OpeningFloatCents).IsRequired();
        builder.Property(s => s.ClosingComment).HasMaxLength(CashShift.CommentMaxLength);

        // Instantánea del bloque "Crédito" (014): nulas en los turnos cerrados antes de 0.9.0 (AddColumn sin reconstruir).
        builder.Property(s => s.OnAccountSalesCents);
        builder.Property(s => s.CustomerPaymentsCashCents);
        builder.Property(s => s.CustomerPaymentsNonCashCents);
        builder.Property(s => s.CustomerPaymentVoidsCashCents);
        builder.Property(s => s.CustomerPaymentVoidsNonCashCents);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.UpdatedBy).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken().IsRequired();

        builder.Ignore(s => s.Folio);
        builder.Ignore(s => s.OpeningFloat);
        builder.Ignore(s => s.DepositsTotalCents);
        builder.Ignore(s => s.WithdrawalsTotalCents);
        builder.Ignore(s => s.Difference);

        // Los turnos y sus movimientos nunca se borran: la relación no se elimina en cascada.
        builder.HasMany(s => s.Movements)
            .WithOne()
            .HasForeignKey(m => m.CashShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.Number).HasDatabaseName(NumberIndexName).IsUnique();

        // Un solo turno abierto por caja (SC-002): el índice hace imposible un segundo aunque falle la aplicación.
        builder.HasIndex(s => s.RegisterCode)
            .HasDatabaseName(OpenPerRegisterIndexName)
            .IsUnique()
            .HasFilter("\"Status\" = 'OPEN'");

        builder.HasIndex(s => new { s.OpenedAt, s.Id }).HasDatabaseName("IX_CashShifts_OpenedAt");
        builder.HasIndex(s => new { s.OpenedBy, s.OpenedAt }).HasDatabaseName("IX_CashShifts_OpenedBy_OpenedAt");
    }
}
