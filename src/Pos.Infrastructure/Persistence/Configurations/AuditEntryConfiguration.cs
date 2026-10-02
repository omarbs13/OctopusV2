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
        builder.Property(a => a.EntityName).HasMaxLength(AuditEntry.EntityNameMaxLength);
        builder.Property(a => a.Details).HasMaxLength(AuditEntry.DetailsMaxLength);
        builder.Property(a => a.Reason).HasMaxLength(AuditEntry.ReasonMaxLength);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();
        builder.Property(a => a.AuthorizedBy);

        // Cambios de campo en una columna JSON de la misma fila (018, research §1): la entrada y sus
        // cambios se escriben juntos y siguen siendo inmutables.
        builder.OwnsMany(a => a.Changes, changes =>
        {
            changes.ToJson("Changes");
            changes.Property(c => c.Field).HasMaxLength(AuditFieldChange.FieldMaxLength).IsRequired();
            changes.Property(c => c.Before).HasMaxLength(AuditFieldChange.ValueMaxLength);
            changes.Property(c => c.After).HasMaxLength(AuditFieldChange.ValueMaxLength);
        });
        builder.Navigation(a => a.Changes).HasField("_changes").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("IX_AuditEntries_Entity");

        // Consulta de la bitácora (007, FR-027; 018, research §10): filtro de igualdad más orden por fecha.
        // (CreatedAt, Id) da el orden de la página sin ordenar en memoria: es el desempate de la consulta.
        builder.HasIndex(a => new { a.CreatedAt, a.Id }).HasDatabaseName("IX_AuditEntries_CreatedAt");
        builder.HasIndex(a => new { a.CreatedBy, a.CreatedAt }).HasDatabaseName("IX_AuditEntries_CreatedBy");
        builder.HasIndex(a => a.AuthorizedBy).HasDatabaseName("IX_AuditEntries_AuthorizedBy");
        builder.HasIndex(a => new { a.Action, a.CreatedAt }).HasDatabaseName("IX_AuditEntries_Action_CreatedAt");
        builder.HasIndex(a => new { a.EntityType, a.CreatedAt }).HasDatabaseName("IX_AuditEntries_EntityType_CreatedAt");
    }
}
