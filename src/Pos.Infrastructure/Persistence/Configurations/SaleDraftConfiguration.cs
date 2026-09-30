using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Borrador de la venta en curso: una sola fila (<c>Slot</c> = 1), sin auditoría (Complexity Tracking).</summary>
internal sealed class SaleDraftConfiguration : IEntityTypeConfiguration<SaleDraft>
{
    public void Configure(EntityTypeBuilder<SaleDraft> builder)
    {
        builder.ToTable("SaleDrafts", t => t.HasCheckConstraint("CK_SaleDrafts_Slot", "\"Slot\" = 1"));
        builder.HasKey(d => d.Slot);
        builder.Property(d => d.Slot).ValueGeneratedNever();
        builder.Property(d => d.DraftId).IsRequired();
        builder.Property(d => d.LinesJson).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();
    }
}
