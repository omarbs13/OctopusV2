using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Application.Abstractions;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Borrador de la venta en curso: una fila por usuario (<c>UserId</c>), sin auditoría (Complexity Tracking).</summary>
internal sealed class SaleDraftConfiguration : IEntityTypeConfiguration<SaleDraft>
{
    public void Configure(EntityTypeBuilder<SaleDraft> builder)
    {
        builder.ToTable("SaleDrafts");
        builder.HasKey(d => d.UserId);

        // El valor por defecto solo sirve para que la reconstrucción de la tabla en la migración
        // UsersAndRoles asigne "Sistema" a la fila anterior; las filas nuevas traen UserId explícito.
        builder.Property(d => d.UserId).ValueGeneratedNever().HasDefaultValue(SystemUser.Id);
        builder.Property(d => d.DraftId).IsRequired();
        builder.Property(d => d.LinesJson).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();
    }
}
