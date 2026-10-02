using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;

namespace Pos.Infrastructure.Persistence.Configurations;

/// <summary>
/// Registro técnico de notificaciones de existencia (022): sin auditoría ni borrado lógico, se purga a
/// los 7 días (Complexity Tracking). Sin claves foráneas, como <c>CategoryId</c>.
/// </summary>
internal sealed class StockAlertAcknowledgementConfiguration : IEntityTypeConfiguration<StockAlertAcknowledgement>
{
    public const string UniqueIndexName = "IX_StockAlertAcknowledgements_User_Date_Level_Product";

    public void Configure(EntityTypeBuilder<StockAlertAcknowledgement> builder)
    {
        builder.ToTable("StockAlertAcknowledgements");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.ProductId).IsRequired();
        builder.Property(a => a.Level).IsRequired();

        // Día calendario local (yyyy-MM-dd), no un instante: clave de "ya se notificó hoy" (research §4).
        builder.Property(a => a.LocalDate).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.LocalDate, a.Level, a.ProductId })
            .HasDatabaseName(UniqueIndexName)
            .IsUnique();
    }
}
