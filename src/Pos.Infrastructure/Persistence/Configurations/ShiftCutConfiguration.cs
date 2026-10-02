using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.CashShifts;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ShiftCutConfiguration : IEntityTypeConfiguration<ShiftCut>
{
    public const string TypeNumberIndexName = "IX_ShiftCuts_Type_Number";
    public const string ClosingPerShiftIndexName = "IX_ShiftCuts_ClosingPerShift";

    public void Configure(EntityTypeBuilder<ShiftCut> builder)
    {
        builder.ToTable("ShiftCuts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Type)
            .HasConversion(type => type.ToCode(), code => ShiftCutTypeExtensions.FromCode(code))
            .HasMaxLength(1)
            .IsRequired();
        builder.Property(c => c.Number).IsRequired();
        builder.Property(c => c.ShiftNumber).IsRequired();
        builder.Property(c => c.RegisterCode).HasMaxLength(CashShift.RegisterCodeMaxLength).IsRequired();
        builder.Property(c => c.ShiftOpenedBy).IsRequired();
        builder.Property(c => c.ShiftOpenedAt).IsRequired();
        builder.Property(c => c.GeneratedAt).IsRequired();
        builder.Property(c => c.GeneratedBy).IsRequired();
        builder.Property(c => c.Comment).HasMaxLength(CashShift.CommentMaxLength);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.UpdatedBy).IsRequired();
        builder.Property(c => c.Version).IsConcurrencyToken().IsRequired();

        builder.Ignore(c => c.Folio);
        builder.Ignore(c => c.ShiftFolio);
        builder.Ignore(c => c.Difference);

        // Los cortes y los turnos nunca se borran. GeneratedBy y AuthorizedBy van sin llave foránea,
        // como OpenedBy y CreatedBy en el resto del proyecto (research §13).
        builder.HasOne<CashShift>()
            .WithMany()
            .HasForeignKey(c => c.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        // Folio consecutivo por tipo, sin repeticiones (FR-010).
        builder.HasIndex(c => new { c.Type, c.Number }).HasDatabaseName(TypeNumberIndexName).IsUnique();

        // Un solo Corte Z por turno.
        builder.HasIndex(c => c.ShiftId, ClosingPerShiftIndexName)
            .IsUnique()
            .HasFilter("\"Type\" = 'Z'");

        builder.HasIndex(c => c.ShiftId, "IX_ShiftCuts_ShiftId");
        builder.HasIndex(c => new { c.GeneratedAt, c.Id }).HasDatabaseName("IX_ShiftCuts_GeneratedAt");
        builder.HasIndex(c => new { c.GeneratedBy, c.GeneratedAt }).HasDatabaseName("IX_ShiftCuts_GeneratedBy_GeneratedAt");
    }
}
