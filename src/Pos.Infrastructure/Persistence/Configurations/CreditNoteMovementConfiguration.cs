using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CreditNotes;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CreditNoteMovementConfiguration : IEntityTypeConfiguration<CreditNoteMovement>
{
    public void Configure(EntityTypeBuilder<CreditNoteMovement> builder)
    {
        builder.ToTable("CreditNoteMovements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.Type)
            .HasConversion(t => t.ToCode(), code => CreditNoteMovementTypeExtensions.FromCode(code))
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(m => m.AmountCents).IsRequired();
        // Sin llaves foráneas hacia ventas ni devoluciones: solo referencia de consulta.
        builder.Property(m => m.SaleId);
        builder.Property(m => m.SaleReturnId);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).IsRequired();

        builder.HasOne<CreditNote>()
            .WithMany()
            .HasForeignKey(m => m.CreditNoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CreditNoteId, m.Sequence })
            .HasDatabaseName("IX_CreditNoteMovements_Note_Sequence")
            .IsUnique();
    }
}
