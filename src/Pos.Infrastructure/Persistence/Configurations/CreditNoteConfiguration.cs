using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CreditNotes;
using Pos.Domain.Returns;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Notas de crédito inmutables: el saldo se calcula con sus movimientos (research §5).</summary>
internal sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public const string NumberIndexName = "IX_CreditNotes_Number";

    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("CreditNotes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Number).IsRequired();
        builder.Property(n => n.InitialCents).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.CreatedBy).IsRequired();
        builder.Ignore(n => n.Folio);

        builder.HasOne<SaleReturn>()
            .WithMany()
            .HasForeignKey(n => n.SaleReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.Number).HasDatabaseName(NumberIndexName).IsUnique();
        builder.HasIndex(n => n.SaleReturnId).HasDatabaseName("IX_CreditNotes_SaleReturnId");
    }
}
