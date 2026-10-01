using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Receivables;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Libro inmutable: solo fecha y usuario de creación, sin borrado lógico ni versión (plan.md, Complexity Tracking).</summary>
internal sealed class ReceivableEntryConfiguration : IEntityTypeConfiguration<ReceivableEntry>
{
    public void Configure(EntityTypeBuilder<ReceivableEntry> builder)
    {
        builder.ToTable("ReceivableEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Type)
            .HasConversion(type => type.ToCode(), code => ReceivableEntryTypeExtensions.FromCode(code))
            .HasMaxLength(14)
            .IsRequired();
        builder.Property(e => e.AmountCents).IsRequired();
        // Referencias de consulta, sin llave foránea: el abono y la devolución tienen su propio registro inmutable.
        builder.Property(e => e.CustomerPaymentId);
        builder.Property(e => e.SaleReturnId);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();

        builder.HasIndex(e => new { e.ReceivableId, e.CreatedAt }).HasDatabaseName("IX_ReceivableEntries_Receivable");
        builder.HasIndex(e => e.CustomerPaymentId).HasDatabaseName("IX_ReceivableEntries_Payment");
    }
}
