using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Audit;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>Bitácora inmutable: solo fecha y usuario de creación (Complexity Tracking).</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Action).HasMaxLength(AuditEntry.ActionMaxLength).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.EntityId).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(AuditEntry.DetailsMaxLength);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("IX_AuditEntries_Entity");
    }
}
